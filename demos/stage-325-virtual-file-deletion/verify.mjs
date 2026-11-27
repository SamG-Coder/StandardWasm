import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-325-virtual-file-deletion");
const evidence = join(root, "artifacts/stage-325-virtual-file-deletion");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath), fixture = readFileSync(oracle.assembly);

async function verify({ compiler, fixture, pathCases }, instantiate) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        bytes.forEach((byte, index) => tool.SetByte(target, index, byte));
        return target;
    }
    const encoder = new TextEncoder();
    const image = upload(decode(fixture));
    const find = name => tool.FindRow(image, upload(encoder.encode(name)));
    const manifest = [];
    for (const name of ["Delete", "Lookup", "SavedError", "Module"]) {
        const bytes = encoder.encode(name);
        const entry = new Uint8Array(8 + bytes.length);
        const view = new DataView(entry.buffer);
        view.setUint32(0, find(name), true);
        view.setUint32(4, bytes.length, true);
        entry.set(bytes, 8);
        manifest.push(...entry);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const getLocation = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const count = tool.CompileModuleDetailed(image, upload(manifest), output, diagnostic);
    if (count < 1) throw Error("Fixture compilation " + count + " at " + getLocation());
    const module = new WebAssembly.Module(Uint8Array.from({ length: count }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(item => item.module + "/" + item.name);
    if (!imports.includes("standardwasm:win32/deleteFileW") || !imports.includes("standardwasm:resources/memory"))
        throw Error("Missing the virtual DeleteFileW ABI");
    const rejected = { status: tool.CompileDetailed(image, find("Wrong"), output, diagnostic), location: getLocation() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, find("WrongPointee"), -1].join())
        throw Error("Unexpected wrong-pointee result " + JSON.stringify(rejected));

    const currentDirectory = "C:\\StandardWasm\\App";
    async function launch(entries) {
        let memory;
        const win32FileSystem = { currentDirectory };
        if (entries) win32FileSystem.longPathMount = { root: currentDirectory, entries };
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
            win32FileSystem,
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (!guest.Module() || !memory) throw Error("Expected production memory import");
        const address = memory.grow(1) * 65536;
        const view = new DataView(memory.buffer);
        function write(text) {
            for (let i = 0; i < text.length; i++) view.setUint16(address + i * 2, text.charCodeAt(i), true);
            view.setUint16(address + text.length * 2, 0, true);
            return address;
        }
        const removeAt = pointer => ({ result: guest.Delete(pointer), error: guest.SavedError() });
        return { remove: text => removeAt(write(text)), removeAt,
            lookup: text => guest.Lookup(write(text), address + 512, 256) };
    }

    const entries = [
        { path: "case.txt", kind: "file" }, { path: "read only.txt", kind: "file", readOnly: true },
        { path: "Folder", kind: "directory" }, { path: "Folder/inside.txt", kind: "file" },
        { path: "wide λ🙂.txt", kind: "file" }, { path: "space.txt", kind: "file" },
        { path: "Long Example File.txt", kind: "file", dosName: "LONGEX~1.TXT" }
    ];
    const instance = await launch(entries);
    const cases = pathCases.map(path => ({ path, ...instance.remove(path) }));
    const aliasWasVisible = instance.lookup("LONGEX~1.TXT") > 0;
    const extendedPrefix = String.fromCharCode(92, 92, 63, 92);
    const deletedAlias = instance.remove(extendedPrefix + currentDirectory + "\\LONGEX~1.TXT");
    const aliasGone = instance.lookup("LONGEX~1.TXT") === 0 && instance.lookup("Long Example File.txt") === 0;
    const other = await launch(entries);
    const isolated = other.lookup("LONGEX~1.TXT") > 0 && other.lookup("case.txt") > 0;
    const empty = await launch();
    return { cases, rejected, aliasWasVisible, deletedAlias, aliasGone, isolated,
        invalidAddress: instance.removeAt(0), noMount: empty.remove("case.txt"),
        outsideMount: instance.remove("C:\\StandardWasm\\Unrelated.txt") };
}

const input = { fixture: fixture.toString("base64"), compiler: compiler.toString("base64"),
    pathCases: oracle.cases.map(item => item.path) };
const node = await verify(input, instantiateManagedModule);
assert.deepEqual(node.cases, oracle.cases);
assert.equal(node.aliasWasVisible, true);
assert.equal(node.aliasGone, true);
assert.equal(node.isolated, true);
assert.deepEqual(node.deletedAlias, { result: 1, error: 0 });
assert.deepEqual(node.invalidAddress, { result: 0, error: 998 });
assert.deepEqual(node.noMount, { result: 0, error: 2 });
assert.deepEqual(node.outsideMount, { result: 0, error: 50 });

const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    const sha = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha(compiler), fixture: sha(fixture), clr: oracle.clr, nodeVersion: process.version,
        edgeVersion: browser.version(), ...node
    }, null, 2) + "\n");
    console.log("Virtual DeleteFileW matched " + node.cases.length + " Windows x86 results in Node and Edge.");
} finally {
    await browser.close();
    await server.close();
}
