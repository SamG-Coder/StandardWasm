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

const stage = join(root, "demos/stage-326-window-message-posts");
const evidence = join(root, "artifacts/stage-326-window-message-posts");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath), fixture = readFileSync(oracle.assembly);

async function exercise(compiler, fixture, { operations, message }, instantiate, createWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const encoder = new TextEncoder();
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const source = upload(fixture);
    const find = name => tool.FindRow(source, upload(encoder.encode(name)));
    const names = ["ThreadId", "ExecutableModule", "SavedError", "SetSavedError", "Post", "PostWindow", "PeekWindow"];
    const entries = [];
    for (const name of names) {
        const bytes = encoder.encode(name);
        const row = find(name);
        if (row < 1) throw Error("Missing " + name);
        const record = new Uint8Array(8 + bytes.length);
        const view = new DataView(record.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, bytes.length, true);
        record.set(bytes, 8);
        entries.push(...record);
    }
    const buffer = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const count = tool.CompileModuleDetailed(source, upload(entries), buffer, diagnostic);
    if (count < 1) throw Error("Fixture compilation " + count + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: count }, (_, i) => tool.ReadByte(buffer, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name }) => module + "/" + name).sort();
    for (const name of ["postMessageW", "postThreadMessageW", "peekMessageW"]) {
        if (imports.filter(entry => entry === "standardwasm:win32/" + name).length !== 1)
            throw Error("Expected a single host import for " + name);
    }
    const rejected = { status: tool.CompileDetailed(source, find("RejectIntHandle"), buffer, diagnostic), location: location() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, find("WrongHandle"), -1].join())
        throw Error("Expected a located diagnostic for the non-native HWND");

    async function launch() {
        const windows = createWindows();
        const handles = { zero: 0, thread: -1, invalid: -99, A: windows.register(0), B: windows.register(0) };
        handles.child = windows.register(0x40000000, undefined, { parent: handles.A });
        let memory;
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (!guest.ExecutableModule() || !memory) throw Error("Missing the launch ABI memory");
        const pointer = memory.grow(1) * 65536;
        const view = new DataView(memory.buffer);
        function peek(filter, flags) {
            for (let word = 0; word < 8; word++) view.setInt32(pointer + word * 4, -1, true);
            const read = word => view.getInt32(pointer + word * 4, true);
            const result = guest.PeekWindow(pointer, handles[filter], message, flags);
            const hwnd = read(0);
            const target = hwnd === -1 ? "unchanged" : Object.keys(handles).find(name => handles[name] === hwnd);
            return { result, error: guest.SavedError(), target, msg: read(1) >>> 0, payload: read(2) >>> 0,
                lParam: read(3), guard: read(7) };
        }
        return { guest, handles, windows, peek };
    }

    const { guest, handles, windows, peek } = await launch();
    const results = operations.map(operation => {
        if (operation.kind === "peek") return peek(operation.filter, operation.flags);
        if (operation.kind === "destroy") return { result: Number(windows.unregister(handles[operation.target])) };
        if (operation.kind === "post-thread")
            return { result: guest.Post(guest.ThreadId(), message, operation.payload, -100, 1), error: guest.SavedError() };
        guest.SetSavedError(789);
        return { result: guest.PostWindow(handles[operation.target], operation.msg, operation.payload, -100, operation.capture),
            error: guest.SavedError() };
    });
    guest.PostWindow(handles.A, message, 100, -100, 1);
    const second = await launch();
    return { results, rejected, imports, isolated: second.peek("zero", 1).result === 0,
        broadcast: { result: guest.PostWindow(0xffff, message, 100, 0, 1), error: guest.SavedError() } };
}

const node = await exercise(compiler, fixture, oracle, instantiateManagedModule, createWindowState);
for (const [index, operation] of oracle.operations.entries())
    assert.deepEqual(node.results[index], operation.expected, `Windows parity for operation ${index} (${operation.kind})`);
assert.equal(node.isolated, true);
assert.deepEqual(node.broadcast, { result: 0, error: 50 });
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/window-posts.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/window-posts.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + source + ")")(...assets, oracle, instantiateManagedModule, createWindowState);
    }, { source: exercise.toString(), oracle });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(fixture), clr: oracle.clr, node: process.version,
        edge: browser.version(), operations: oracle.operations, ...node
    }, null, 2) + "\n");
    console.log("Window and thread posting matched " + node.results.length + " Windows x86 results in Node and Edge.");
} finally {
    await browser.close();
    await server.close();
}
