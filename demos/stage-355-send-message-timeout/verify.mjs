import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const name = "stage-355-send-message-timeout";
const stage = join(root, "demos", name), evidence = join(root, "artifacts", name);
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath), fixture = readFileSync(oracle.assembly);

async function exercise(compiler, fixture, operations, instantiate, createWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const encoder = new TextEncoder();
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(array, i, bytes[i]);
        return array;
    }
    const source = upload(fixture);
    const row = name => tool.FindRow(source, upload(encoder.encode(name)));
    const entries = ["ExecutableModule", "SendWindow", "SetErrors", "SystemError", "SavedError"].flatMap(name => {
        const bytes = encoder.encode(name), entry = new Uint8Array(8 + bytes.length);
        const view = new DataView(entry.buffer);
        view.setUint32(0, row(name), true);
        view.setUint32(4, bytes.length, true);
        entry.set(bytes, 8);
        return [...entry];
    });
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileModuleDetailed(source, upload(entries), output, diagnostic);
    if (size < 1) throw Error("Send compilation " + size + " at " + location());
    const module = await WebAssembly.compile(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module);
    if (imports.filter(item => item.module === "standardwasm:win32" && item.name === "sendMessageTimeoutW").length !== 1 ||
        imports.filter(item => item.kind === "memory").length !== 1) throw Error("Wrong send import shape");
    const rejected = { status: tool.CompileDetailed(source, row("RejectWrongResult"), output, diagnostic), location: location() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, row("WrongResult"), -1].join())
        throw Error("Expected a diagnostic for the wrong output word: " + JSON.stringify(rejected));

    const windows = createWindows();
    let guest, memory, errors, deliveries = 0;
    function open() {
        const window = windows.register(0, (message, wParam, lParam) => {
            deliveries++;
            if (message === 0x8043) memory.grow(1);
            if (message === 0x8044) windows.unregister(window);
            if (message === 0x8045) errors.system = 333;
            if (message === 0x8046 && guest.SendWindow(window, 0x8042, 1, 2, 0, 0, 0, 0) !== 1)
                throw Error("The nested send failed.");
            return wParam ^ lParam;
        });
        return window;
    }
    const handles = { window: open(), closed: open(), destroyPlain: open(), destroyFlag: open(), null: 0, invalid: -99 };
    windows.unregister(handles.closed);
    ({ exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries(registry) { ({ memory, errors } = registry); }
    }));
    if (!guest.ExecutableModule() || !memory) throw Error("Missing guest memory");
    const pointer = memory.grow(1) * 65536;
    const view = () => new DataView(memory.buffer);
    function observe({ target, message, wParam, lParam, flags, timeout, write, capture }) {
        deliveries = 0;
        view().setInt32(pointer, 0x13572468, true);
        view().setInt32(pointer + 4, 0x12345678, true);
        guest.SetErrors(741, 789);
        const status = guest.SendWindow(handles[target], message, wParam, lParam, flags, timeout, write ? pointer : 0, capture);
        if (view().getInt32(pointer + 4, true) !== 0x12345678) throw Error("Overwrote the result guard.");
        return { status, system: guest.SystemError(), saved: guest.SavedError(), result: view().getInt32(pointer, true), deliveries };
    }
    const results = operations.map(observe);
    deliveries = 0;
    const bounded = guest.SendWindow(handles.window, 0x8042, 1, 2, 0, 1, memory.buffer.byteLength - 2, 0);
    if (bounded !== 0 || guest.SystemError() !== 998 || deliveries !== 0) throw Error("Invalid output did not stop dispatch.");
    return { results, rejected };
}

const node = await exercise(compiler, fixture, oracle.operations, instantiateManagedModule, createWindowState);
assert.deepEqual(node.results, oracle.operations.map(operation => operation.expected));
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/send-guest.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, operations }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/send-guest.dll"].map(async url => {
            const response = await fetch(url);
            if (!response.ok) throw Error(url + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + source + ")")(...assets, operations, instantiateManagedModule, createWindowState);
    }, { source: exercise.toString(), operations: oracle.operations });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const result = { compiler: sha256(compiler), fixture: sha256(fixture), clr: oracle.clr,
        node: process.version, edge: browser.version(), operations: oracle.operations, ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log("Same-queue window sends matched " + node.results.length + " Windows x86 results in Node and Edge.");
} finally {
    await browser.close();
    await server.close();
}
