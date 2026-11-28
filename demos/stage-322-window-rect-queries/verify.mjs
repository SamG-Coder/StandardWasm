import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import binaryen from "binaryen";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-322-window-rect-queries");
const evidence = join(root, "artifacts/stage-322-window-rect-queries");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const abi = binaryen.parseText(readFileSync(join(stage, "../stage-318-message-only-windows/abi.wat"), "utf8"));
assert.equal(abi.validate(), 1);
const abiBytes = abi.emitBinary();
abi.dispose();

async function exercise(compiler, fixture, abiBytes, { requests, metrics }, instantiate, createWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const encoder = new TextEncoder();
    const source = upload(fixture);
    const find = name => tool.FindRow(source, upload(encoder.encode(name)));
    const entries = [];
    for (const name of ["Window", "Client", "SavedError", "ExecutableModule"]) {
        const bytes = encoder.encode(name);
        const record = new Uint8Array(8 + bytes.length);
        const view = new DataView(record.buffer);
        view.setUint32(0, find(name), true);
        view.setUint32(4, bytes.length, true);
        record.set(bytes, 8);
        entries.push(...record);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileModuleDetailed(source, upload(entries), output, diagnostic);
    if (size < 1) throw Error("Fixture compilation " + size + " at " + location());
    const queries = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const importNames = WebAssembly.Module.imports(queries).map(item => item.module + "/" + item.name).sort();
    for (const name of ["getWindowRect", "getClientRect"]) {
        if (!importNames.includes("standardwasm:win32/" + name)) throw Error("Missing named import " + name);
    }
    const rejected = { status: tool.CompileDetailed(source, find("Wrong"), output, diagnostic), location: location() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, find("WrongPointee"), -1].join())
        throw Error("Unexpected ABI diagnostic " + JSON.stringify(rejected));

    const windows = createWindows();
    let queryMemory;
    const { exports: queryGuest } = await instantiate(queries, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries({ memory }) { queryMemory = memory; }
    });
    if (!queryGuest.ExecutableModule() || !queryMemory) throw Error("Missing query ABI memory");
    const pointer = queryMemory.grow(1) * 65536;
    const view = new DataView(queryMemory.buffer);
    function call(method, hwnd, outputAddress = pointer) {
        [11, 22, 33, 44, 55].forEach((value, i) => view.setInt32(pointer + i * 4, value, true));
        const result = method(hwnd, outputAddress);
        return { result, error: queryGuest.SavedError(),
            words: [0, 4, 8, 12, 16].map(offset => view.getInt32(pointer + offset, true)) };
    }
    const query = hwnd => ({ window: call(queryGuest.Window, hwnd), client: call(queryGuest.Client, hwnd) });

    const trace = [];
    let guest, registry, fallback;
    const signature = "stdcall:i32(p32,u32,u32,i32)";
    const { exports } = await instantiate(new WebAssembly.Module(Uint8Array.from(abiBytes)), {}, [], {}, "Fixture.exe",
        windows, undefined, {
            win32Desktop: true,
            windowCreationMetrics: metrics,
            configureNativeLibraries(libraries) {
                registry = libraries;
                registry.registerLibrary("stage322", new Map([["Procedure", {
                    signature, invoke(hwnd, message, wParam, lParam) {
                        if ([0x24, 0x81, 0x83, 1].includes(message)) trace.push({ message, before: query(hwnd) });
                        return guest.callWindowProcW(fallback, hwnd, message, wParam, lParam);
                    }
                }]]));
            }
        });
    guest = exports;
    const base = guest.memory.grow(1) * 65536;
    const className = base + 64, user32 = base + 160, symbol = base + 192;
    const abiView = new DataView(guest.memory.buffer);
    for (const [address, text] of [[className, "StandardWasm.RectQueries"], [user32, "user32.dll"]])
        for (let i = 0; i < text.length; i++) abiView.setUint16(address + i * 2, text.charCodeAt(i), true);
    new Uint8Array(guest.memory.buffer).set(encoder.encode("DefWindowProcW"), symbol);
    fallback = guest.getProcAddress(guest.getModuleHandleW(user32), symbol);
    const callback = registry.getProcAddress(registry.loadLibrary("stage322"), "Procedure");
    [0, callback, 0, 0, 1, 0, 0, 0, 0, className].forEach((value, i) => abiView.setInt32(base + i * 4, value, true));
    const atom = guest.registerClassW(base);
    if (!atom || !fallback) throw Error("Could not register the guest window class");

    function create({ ExStyle, Style, X, Y, Width, Height }) {
        const hwnd = guest.createWindowExW(ExStyle, atom, 0, Style, X, Y, Width, Height, 0, 0, 1, 0);
        if (!hwnd) throw Error("Fixture HWND was rejected");
        return hwnd;
    }
    const outcomes = requests.map(request => {
        trace.length = 0;
        const hwnd = create(request);
        const created = query(hwnd);
        windows.unregister(hwnd);
        return { callbacks: trace.slice(), created, retired: query(hwnd) };
    });
    const hwnd = create(requests[0]);
    return { outcomes, invalid: query(-1), rejected, importNames, badPointers: [
        call(queryGuest.Window, hwnd, 0), call(queryGuest.Client, hwnd, queryMemory.buffer.byteLength - 15)
    ] };
}

const node = await exercise(compiler, fixture, abiBytes, oracle, instantiateManagedModule, createWindowState);
assert.deepEqual(node.outcomes, oracle.outcomes);
assert.deepEqual(node.invalid, oracle.invalid);
assert.deepEqual(node.badPointers, Array(2).fill({ result: 0, error: 998, words: [11, 22, 33, 44, 55] }));
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/rectangle-fixture.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, abiBytes, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/rectangle-fixture.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + source + ")")(...assets, abiBytes, oracle, instantiateManagedModule, createWindowState);
    }, { source: exercise.toString(), abiBytes: [...abiBytes], oracle });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const comparisons = oracle.outcomes.reduce((total, item) => total + 2 * (item.callbacks.length + 2), 2);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(fixture), oracle: oracle.clr, node: process.version,
        edge: browser.version(), comparisons, ...node
    }, null, 2) + "\n");
    console.log(JSON.stringify({ comparisons, clr: oracle.clr, node: process.version, edge: browser.version() }));
} finally {
    await browser.close();
    await server.close();
}
