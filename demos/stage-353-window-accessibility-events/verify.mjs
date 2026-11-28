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

const name = "stage-353-window-accessibility-events";
const stage = join(root, "demos", name);
const evidence = join(root, "artifacts", name);
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
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const source = upload(fixture);
    const find = method => tool.FindRow(source, upload(encoder.encode(method)));
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileDetailed(source, find("Notify"), output, diagnostic);
    if (size < 1) throw Error("Notify compilation " + size + " at " + location());
    const module = await WebAssembly.compile(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(item => item.module + "/" + item.name + "/" + item.kind);
    if (imports.join() !== "standardwasm:win32/notifyWinEvent/function") throw Error("Unexpected imports: " + imports);
    const rejected = { status: tool.CompileDetailed(source, find("RejectWrongReturn"), output, diagnostic), location: location() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, find("WrongReturn"), -1].join())
        throw Error("Expected located failure for a false return ABI: " + JSON.stringify(rejected));

    const events = [];
    const windows = createWindows();
    const handles = { window: windows.register(0), null: 0, invalid: -99, destroyed: windows.register(0) };
    windows.unregister(handles.destroyed);
    const owner = windows.getWindowOwner(handles.window);
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
        onWinEvent({ eventCode, hwnd, objectId, childId, threadId, processId }) {
            if (threadId !== owner.threadId || processId !== owner.processId) throw Error("Wrong event owner");
            const window = Object.keys(handles).find(key => handles[key] === hwnd);
            events.push({ eventCode, window, objectId, childId });
        }
    });
    for (const { eventCode, window, objectId, childId } of operations) {
        if (guest.run(eventCode, handles[window], objectId, childId) !== undefined) throw Error("Expected a void return");
    }
    const before = events.length;
    const secondWindows = createWindows(), secondHandle = secondWindows.register(0);
    const { exports: unobserved } = await instantiate(module, tool, [], {}, "Other.exe", secondWindows);
    unobserved.run(0x800c, secondHandle, -4, 107);
    if (events.length !== before) throw Error("A different launch received the notification.");
    return { imports, rejected, events };
}

const node = await exercise(compiler, fixture, oracle.operations, instantiateManagedModule, createWindowState);
assert.deepEqual(node.events, oracle.events);
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/notify-guest.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, operations }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/notify-guest.dll"].map(async url => {
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
    console.log(JSON.stringify(result));
} finally {
    await browser.close();
    await server.close();
}
