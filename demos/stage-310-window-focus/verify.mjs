import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-310-window-focus"), evidence = join(root, "artifacts/stage-310-window-focus");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8")), image = readFileSync(oracle.assembly);
const cases = oracle.observations.map(({ operation, id, error }) => ({ operation, id, checkError: error !== null }));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, image, cases, instantiate, createRegistry) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(target, index, bytes[index]);
        return target;
    }
    const source = upload(image), encode = name => new TextEncoder().encode(name);
    const row = name => tool.FindRow(source, upload(encode(name)));
    const entries = [];
    for (const name of ["Focus", "Active", "Activate", "Pick", "Enable", "SavedError"]) {
        const method = row(name);
        if (method < 1) throw Error(`Missing fixture entry ${name}`);
        const text = encode(name), entry = new Uint8Array(text.length + 8), header = new DataView(entry.buffer);
        header.setUint32(0, method, true);
        header.setUint32(4, text.length, true);
        entry.set(text, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileModuleDetailed(source, upload(entries), output, diagnostic);
    if (size < 1) throw Error(`Fixture compilation ${size} at ${location()}`);
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name, kind }) => `${module}/${name}/${kind}`).sort();
    const wrong = row("WrongReturn"), caller = row("RejectWrongReturn");
    const status = tool.CompileDetailed(source, caller, output, diagnostic);
    const rejection = { status, location: location(), expected: [0, wrong, -1] };

    const windows = createRegistry(), messages = [];
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows);
    const handles = new Map([[0, 0], [-1, -1]]), ids = new Map([[0, 0]]);
    const identify = handle => ids.get(handle >>> 0) ?? -1;
    const active = () => identify(guest.Active()), focus = () => identify(guest.Focus());
    for (const [id, parent, style] of [[1, 0, 0x80000000], [2, 0, 0x80000000], [3, 0, 0x88000000],
        [4, 1, 0x40000000], [5, 4, 0x40000000]]) {
        const handle = windows.register(style, (message, other) => {
            if (message === 7 || message === 8) messages.push([id, message, identify(other), active(), focus()]);
        }, { parent: handles.get(parent) });
        handles.set(id, handle);
        ids.set(handle, id);
    }
    const observations = cases.map(({ operation, id, checkError }) => {
        const handle = handles.get(id);
        let previous = 0;
        if (operation === "activate") previous = identify(guest.Activate(handle));
        else if (operation === "focus") previous = identify(guest.Pick(handle));
        else if (operation === "enable" || operation === "disable") previous = guest.Enable(handle, operation === "enable" ? 1 : 0);
        else if (operation === "destroy" && !windows.unregister(handle)) throw Error(`Missing registered HWND ${id}`);
        return { operation, id, previous, active: active(), focus: focus(), error: checkError ? guest.SavedError() : null,
            messages: messages.splice(0) };
    });
    guest.Pick(handles.get(2));
    const { exports: isolated } = await instantiate(module, tool, [], {}, "Fixture.exe");
    return { observations, independent: [isolated.Active(), isolated.Focus()], imports, rejection };
}

const node = await exercise(compiler, image, cases, instantiateManagedModule, createWindowState);
assert.equal(node.observations.length, oracle.observations.length);
for (let index = 0; index < node.observations.length; index++)
    assert.deepEqual(node.observations[index], oracle.observations[index], `Windows focus step ${index}`);
assert.deepEqual(node.independent, [0, 0]);
assert.equal(node.rejection.status, -2);
assert.deepEqual(node.rejection.location, node.rejection.expected);
assert.deepEqual(node.imports, ["enableWindow", "getActiveWindow", "getFocus", "getLastPInvokeError", "getLastSystemError",
    "setActiveWindow", "setFocus", "setLastPInvokeError", "setLastSystemError"].map(name => `standardwasm:win32/${name}/function`));
const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/window-focus.dll", route => route.fulfill({ body: image, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ code, cases }) => {
        const [{ instantiateManagedModule }, { createWindowState }] = await Promise.all([
            import("/launch/resource-host.mjs"), import("/launch/window-state.mjs")
        ]);
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/window-focus.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(`${path} returned HTTP ${response.status}`);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)(`(${code})`)(...assets, cases, instantiateManagedModule, createWindowState);
    }, { code: exercise.toString(), cases });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(image), clr: oracle.runtime,
        node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(`${node.observations.length} focus observations and ${node.observations.flatMap(item => item.messages).length} ordered messages matched Windows, Node and Edge.`);
} finally {
    await browser?.close();
    await server.close();
}
