import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-254-virtual-long-paths");
const evidence = join(root, "artifacts/stage-254-virtual-long-paths");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(oracle.fixture);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));

async function verify({ compiler, image, entries, cases }, instantiate) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const result = tool.CreateBytes(bytes.length);
        bytes.forEach((byte, index) => tool.SetByte(result, index, byte));
        return result;
    }
    const input = upload(decode(image));
    const row = tool.FindRow(input, upload(new TextEncoder().encode("Observe")));
    if (row < 1) throw Error("Missing fixture entry");
    const output = tool.CreateBytes(1 << 18), diagnostic = tool.CreateDiagnostic();
    const length = tool.CompileDetailed(input, row, output, diagnostic);
    if (length < 1) throw Error("Compiler " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module);
    if (!imports.some(entry => entry.module === "standardwasm:win32" && entry.name === "getLongPathNameW") ||
        !imports.some(entry => entry.module === "standardwasm:resources" && entry.name === "memory"))
        throw Error("Expected the production long-path Win32 import and memory.");
    const directory = "C:\\StandardWasm\\App";
    const compilerAssets = [{ path: "FrameworkOnly.dll", bytes: new Uint8Array([0]) }];
    function launch(mounted, cwd = directory) {
        const win32FileSystem = { currentDirectory: cwd };
        if (mounted) win32FileSystem.longPathMount = { root: directory, entries: mounted };
        return instantiate(module, tool, compilerAssets, {}, "Fixture.exe", undefined, undefined, { win32FileSystem });
    }
    const { exports: first } = await launch(entries);
    const results = Object.fromEntries(cases.map(index => [index, first.run(Number(index)).toString()]));
    const { exports: empty } = await launch(undefined);
    const { exports: second } = await launch([...entries, { path: "MountedOnly.txt", kind: "file" }]);
    const { exports: nested } = await launch(entries, directory + "\\Long Project Folder");
    // Instantiate a different namespace and then re-query the original scope.
    let conflict = false;
    try {
        await launch([...entries, { path: "longpr~7", kind: "file" }]);
    } catch (error) {
        conflict = error instanceof TypeError && error.message.includes("conflicting");
    }
    const noMemory = new WebAssembly.Module(new Uint8Array([
        0, 97, 115, 109, 1, 0, 0, 0,
        1, 8, 1, 96, 3, 127, 127, 127, 1, 127,
        2, 39, 1, 18,
        ...new TextEncoder().encode("standardwasm:win32"),
        16, ...new TextEncoder().encode("getLongPathNameW"), 0, 0
    ]));
    let requiresMemory = false;
    try {
        await instantiate(noMemory, tool, [], {}, "Fixture.exe");
    } catch (error) {
        requiresMemory = error.message.includes("memory import");
    }
    function unpack(run, index) {
        const result = run(index);
        return { length: Number(result & 65535n), error: Number(result >> 16n & 65535n) };
    }
    return { results, browserOnly: {
        noMount: unpack(empty.run, 0),
        noCompilerAssetMount: unpack(first.run, 18),
        firstOnly: unpack(first.run, 19),
        ownMountedFile: unpack(second.run, 19),
        unsupportedName: unpack(first.run, 20),
        bounds: unpack(first.run, 22),
        overlap: unpack(first.run, 23),
        absolute: unpack(first.run, 24),
        extended: unpack(first.run, 25),
        nestedDirectory: unpack(nested.run, 26),
        outsideMount: unpack(first.run, 27),
        parentTraversal: unpack(first.run, 28),
        conflict, requiresMemory
    } };
}

const input = { image: image.toString("base64"), compiler: compiler.toString("base64"),
    entries: oracle.entries, cases: Object.keys(oracle.results) };
const node = await verify(input, instantiateManagedModule);
assert.deepEqual(node.results, oracle.results);
assert.ok(Object.values(node.results).every(value => value !== "-1"), "Raw calls must preserve saved P/Invoke error.");
assert.deepEqual(node.browserOnly, {
    noMount: { length: 0, error: 50 },
    noCompilerAssetMount: { length: 0, error: 2 },
    firstOnly: { length: 0, error: 2 },
    ownMountedFile: { length: 15, error: 17185 },
    unsupportedName: { length: 0, error: 50 },
    bounds: { length: 0, error: 998 },
    overlap: { length: 0, error: 50 },
    absolute: { length: 66, error: 17185 },
    extended: { length: 70, error: 17185 },
    nestedDirectory: { length: "Wide Name Example.document".length, error: 17185 },
    outsideMount: { length: 0, error: 3 },
    parentTraversal: { length: 0, error: 50 },
    conflict: true,
    requiresMemory: true
});
const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    const sha = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha(compiler), fixture: sha(image), clr: oracle.clr, nodeVersion: process.version,
        edgeVersion: browser.version(), oracle: oracle.results, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 on " + input.cases.length + " production long-path cases.");
} finally {
    await browser?.close();
    await server.close();
}
