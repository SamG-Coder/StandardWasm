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

const stage = join(root, "demos/stage-309-active-window"), evidence = join(root, "artifacts/stage-309-active-window");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8")), image = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, image, instantiate, createRegistry) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(target, index, bytes[index]);
        return target;
    }
    const source = upload(image), encode = name => new TextEncoder().encode(name);
    const row = name => tool.FindRow(source, upload(encode(name)));
    const entries = [];
    for (const name of ["Query", "Activate", "SavedError"]) {
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
    const windows = createRegistry();
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows);
    const first = windows.register(0x80000000), second = windows.register(0x80000000);
    const disabled = windows.register(0x88000000), child = windows.register(0x40000000, undefined, { parent: first });
    const keys = new Map([[0, 0], [first, 1], [second, 2], [disabled, 3], [child, 4]]);
    const read = () => keys.get(guest.Query() >>> 0) ?? -1;
    const pick = handle => ({ previous: keys.get(guest.Activate(handle | 0) >>> 0) ?? -1, active: read() });
    const empty = read(), firstSet = pick(first), repeated = pick(first), secondSet = pick(second), invalid = pick(-1);
    const invalidError = guest.SavedError();
    const disabledSet = pick(disabled), childSet = pick(child), zero = pick(0), restored = pick(first);
    const { exports: isolated } = await instantiate(module, tool, [], {}, "Fixture.exe");
    const independent = isolated.Query();
    windows.unregister(child);
    windows.unregister(first);
    const destroyed = read();
    return { outcomes: { empty, firstSet, repeated, secondSet, invalid, invalidError, disabledSet, childSet, zero, restored, destroyed },
        independent, imports, rejection };
}

const node = await exercise(compiler, image, instantiateManagedModule, createWindowState);
assert.deepEqual(node.outcomes, oracle.outcomes);
assert.equal(node.independent, 0);
assert.equal(node.rejection.status, -2);
assert.deepEqual(node.rejection.location, node.rejection.expected);
assert.deepEqual(node.imports, ["getActiveWindow", "getLastPInvokeError", "getLastSystemError", "setActiveWindow",
    "setLastPInvokeError", "setLastSystemError"].map(name => `standardwasm:win32/${name}/function`));
const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/active-window.dll", route => route.fulfill({ body: image, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async source => {
        const [{ instantiateManagedModule }, { createWindowState }] = await Promise.all([
            import("/launch/resource-host.mjs"), import("/launch/window-state.mjs")
        ]);
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/active-window.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(`${path} returned HTTP ${response.status}`);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)(`(${source})`)(...assets, instantiateManagedModule, createWindowState);
    }, exercise.toString());
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(image), clr: oracle.runtime,
        node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify(report));
} finally {
    await browser?.close();
    await server.close();
}
