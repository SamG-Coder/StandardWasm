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

const stage = join(root, "demos/stage-359-window-visibility");
const evidence = join(root, "artifacts/stage-359-window-visibility");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, image, instantiate, createRegistry) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(target, index, bytes[index]);
        return target;
    }
    const source = upload(image);
    const row = name => tool.FindRow(source, upload(new TextEncoder().encode(name)));
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const query = row("Query");
    if (query < 1) throw Error("Missing Query export");
    const size = tool.CompileDetailed(source, query, output, diagnostic);
    if (size < 1) throw Error("Fixture compilation " + size + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module);
    if (imports.length !== 1 || imports[0].module !== "standardwasm:win32" ||
        imports[0].name !== "isWindowVisible" || imports[0].kind !== "function")
        throw Error("Unexpected IsWindowVisible fixture imports " + JSON.stringify(imports));

    const wrongWord = row("WrongWord"), rejectWord = row("RejectWrongWord");
    const rejectWrapper = row("RejectWrongWrapper");
    if (wrongWord < 1 || rejectWord < 1 || rejectWrapper < 1) throw Error("Missing negative fixture");
    // The malformed raw declaration never binds. The wrong nominal wrapper
    // reaches signature checking while linking the call at IL_0006.
    const rejection = [[rejectWord, [0, wrongWord, -1]], [rejectWrapper, [0, rejectWrapper, 6]]]
        .map(([caller, expected]) => {
            const status = tool.CompileDetailed(source, caller, output, diagnostic);
            return { status, actual: location(), expected };
        });

    const windows = createRegistry();
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows);
    const queryWindow = handle => guest.run(handle | 0);
    const hidden = windows.register(0x88000000);
    const visible = windows.register(0x90000000);
    const cover = windows.register(0x90000000);
    for (const handle of [visible, cover]) windows.setWindowGeometry(handle, [20, 20, 28, 28], [20, 20, 28, 28]);
    const child = windows.register(0x50000000, undefined, { parent: visible });
    const grandchild = windows.register(0x50000000, undefined, { parent: child });
    const hiddenChild = windows.register(0x40000000, undefined, { parent: visible });
    const childUnderHidden = windows.register(0x50000000, undefined, { parent: hidden });
    const grandchildUnderHidden = windows.register(0x50000000, undefined, { parent: childUnderHidden });
    const hiddenMiddle = windows.register(0x50000000, undefined, { parent: hiddenChild });
    const ownedPopup = windows.register(0x90000000, undefined, { parent: hidden });
    const results = {
        hidden: queryWindow(hidden), covered: queryWindow(visible), covering: queryWindow(cover),
        child: queryWindow(child), grandchild: queryWindow(grandchild), hiddenChild: queryWindow(hiddenChild),
        childUnderHidden: queryWindow(childUnderHidden), grandchildUnderHidden: queryWindow(grandchildUnderHidden),
        hiddenMiddle: queryWindow(hiddenMiddle), ownedPopup: queryWindow(ownedPopup),
        nullHandle: queryWindow(0), invalidHandle: queryWindow(-1)
    };
    windows.unregister(grandchild);
    windows.unregister(cover);
    results.staleChild = queryWindow(grandchild);
    results.staleTopLevel = queryWindow(cover);
    results.remainingWindow = queryWindow(visible);
    const { exports: isolated } = await instantiate(module, tool, [], {}, "Fixture.exe");
    return { results, imports, rejection, isolated: isolated.run(visible | 0) };
}

const node = await exercise(compiler, image, instantiateManagedModule, createWindowState);
assert.deepEqual(node.results, oracle.results);

for (const { status, actual, expected } of node.rejection) {
    assert.equal(status, -2);
    assert.deepEqual(actual, expected);
}
assert.equal(node.isolated, 0);

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/fixture.dll", route => route.fulfill({ body: image, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async code => {
        const [{ instantiateManagedModule }, { createWindowState }] = await Promise.all([
            import("/launch/resource-host.mjs"), import("/launch/window-state.mjs")
        ]);
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + code + ")")(...assets, instantiateManagedModule, createWindowState);
    }, exercise.toString());
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(image),
        clr: oracle.runtime + "/" + oracle.architecture, node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify(report));
} finally {
    await browser?.close();
    await server.close();
}
