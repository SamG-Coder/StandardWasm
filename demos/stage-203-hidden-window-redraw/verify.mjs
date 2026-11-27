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

const stage = join(root, "demos/stage-203-hidden-window-redraw");
const evidence = join(root, "artifacts/stage-203-hidden-window-redraw");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/HiddenWindowRedraw.dll"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));

async function exercise({ compiler, image, oracle }, instantiate, createWindows) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const data = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(data, i, bytes[i]);
        return data;
    }
    const source = upload(decode(image));
    const encoder = new TextEncoder();
    const entries = [];
    for (const name of ["ExecutableModule", "Redraw", "Error", "SetErrors", "SavedError"]) {
        const encoded = encoder.encode(name);
        const row = tool.FindRow(source, upload(encoded));
        if (row < 1) throw Error("Missing export " + name);
        const entry = new Uint8Array(8 + encoded.length), view = new DataView(entry.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, encoded.length, true);
        entry.set(encoded, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), location = tool.CreateDiagnostic();
    const count = tool.CompileModuleDetailed(source, upload(entries), output, location);
    if (count < 1) throw Error("Fixture compilation " + count + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(location, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: count }, (_, i) => tool.ReadByte(output, i)));
    if (!WebAssembly.Module.imports(module).some(entry => entry.module === "standardwasm:win32" && entry.name === "redrawWindow"))
        throw Error("The compiler did not bind the raw RedrawWindow import");

    const windows = createWindows();
    let memory, paintMessages = 0;
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries(registry) { memory = registry.memory; }
    });
    if (!guest.ExecutableModule()) throw Error("Missing executable module");
    const hwnd = windows.register(0x80000000 | 0, () => { paintMessages++; });
    const visual = windows.register(0x10000000, () => { paintMessages++; });
    const address = memory.grow(1) * 65536;
    const view = new DataView(memory.buffer);
    function redraw(handle, rectangle, region, flags) {
        guest.SetErrors(oracle.system, oracle.saved);
        const result = guest.Redraw(handle, rectangle, region, flags);
        return { result, error: guest.Error(), saved: guest.SavedError() };
    }
    const cases = oracle.cases.map(({ flags, rect }) => {
        rect?.forEach((value, i) => view.setInt32(address + 4 * i, value, true));
        const outcome = redraw(hwnd, rect ? address : 0, 0, flags);
        const unchanged = !rect || rect.every((value, i) => view.getInt32(address + i * 4, true) === value);
        return { flags, rect, ...outcome, unchanged };
    });
    const unsupported = [
        redraw(visual, 0, 0, 1), redraw(hwnd, 0, 9, 1),
        redraw(hwnd, 0, 0, 0x2000), redraw(0, 0, 0, 1)
    ];
    const badRectangle = redraw(hwnd, memory.buffer.byteLength - 15, 0, 1);
    windows.unregister(hwnd);
    const stale = redraw(hwnd, 0, 0, 1);
    return { cases, paintMessages, stale, unsupported, badRectangle };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64"), oracle };
const node = await exercise(input, instantiateManagedModule, createWindowState);
assert.ok(oracle.cases.every(test => test.pending === false));
assert.deepEqual(node.cases, oracle.cases.map(({ pending, ...test }) => test));
assert.equal(node.paintMessages, oracle.paintMessages);
assert.deepEqual(node.stale, oracle.stale);
assert.deepEqual(node.unsupported, Array(4).fill({ result: 0, error: 50, saved: oracle.saved }));
assert.deepEqual(node.badRectangle, { result: 0, error: 998, saved: oracle.saved });

const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, code }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + code + ")")(input, instantiateManagedModule, createWindowState);
    }, { input, code: exercise.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"), clr: runtime.version,
        nodeVersion: process.version, edgeVersion: browser.version(), oracle, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 for " + node.cases.length + " hidden RedrawWindow cases and a stale HWND.");
} finally {
    await browser?.close();
    await server.close();
}
