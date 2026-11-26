import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";

const stage = join(root, "demos", "stage-136-method-instantiations");
const base = join(root, "artifacts", "method-instantiations");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const oracle = join(evidence, "oracle.json"), reader = join(evidence, "Reader.wasm");
const executionOracle = join(evidence, "execution.json"), compiler = join(evidence, "Compiler.wasm");

for (const project of [join(stage, "Fixture"), join(stage, "Reader"), join(root, "tools", "PortableCompiler.Driver")])
    run("dotnet", ["build", project, "-c", "Release"]);
const driver = join(root, "tools", "PortableCompiler.Driver", "bin", "Release", "net10.0", "PortableCompiler.Driver.dll");
const built = project => join(stage, project, "bin", "Release", "netstandard2.0");
run("dotnet", [driver, join(built("Reader"), "Reader.dll"), "MethodSpecProbe", reader]);
const bootstrap = join(root, "demos", "stage-13-complete-bootstrap", "bin", "Release", "netstandard2.0", "BootstrapLibrary.dll");
run("dotnet", [driver, bootstrap, "BootstrapLibrary.CompilerExports", compiler]);

const runtime = wasm32Runtime();
const core = "System.Private.CoreLib.dll";
const fixture = "MethodSpecFixture.dll", dependency = "MethodSpecDependency.dll";
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built("Fixture"), fixture), join(runtime.directory, core), join(runtime.widePack, core), oracle, executionOracle,
    join(built("Fixture"), dependency)]);
const cases = JSON.parse(readFileSync(oracle, "utf8"));
const execution = JSON.parse(readFileSync(executionOracle, "utf8"));
const assets = new Map([["/reader", readFileSync(reader)], ["/compiler", readFileSync(compiler)]]);
for (const { source, path } of cases)
    if (!assets.has("/" + source)) assets.set("/" + source, readFileSync(path));
const tests = cases.map(({ source, row }) => ({ source, row }));
const [bundle] = packEntry({ path: fixture, bytes: assets.get("/fixture") },
    [{ path: dependency, bytes: readFileSync(join(built("Fixture"), dependency)) }], execution.entries[0].row);
assets.set("/bundle", bundle);

async function inspect(get, tests) {
    const { instance: { exports: reader } } = await WebAssembly.instantiate(await get("/reader"));
    const images = new Map();
    const results = [];
    for (const { source, row } of tests) {
        if (!images.has(source)) {
            const bytes = new Uint8Array(await get("/" + source));
            const image = reader.Create(bytes.length);
            for (let i = 0; i < bytes.length; i++) reader.Write(image, i, bytes[i]);
            images.set(source, image);
        }
        const record = reader.Inspect(images.get(source), row);
        const values = Array.from({ length: reader.ResultLength(record) }, (_, index) => reader.Read(record, index));
        results.push(values);
    }
    return results;
}

async function execute(get, execution) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(await get("/compiler"));
    function upload(bytes) {
        const input = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(input, i, bytes[i]);
        return input;
    }
    const encoder = new TextEncoder();
    const names = execution.entries.map(({ name }) => encoder.encode(name));
    const manifest = new Uint8Array(names.reduce((size, bytes) => size + 8 + bytes.length, 0));
    const view = new DataView(manifest.buffer);
    let offset = 0;
    for (const [index, { row }] of execution.entries.entries()) {
        const name = names[index];
        view.setUint32(offset, row, true);
        view.setUint32(offset + 4, name.length, true);
        manifest.set(name, offset + 8);
        offset += 8 + name.length;
    }
    const output = compiler.CreateBytes(1 << 20);
    const location = compiler.CreateDiagnostic();
    const size = compiler.CompileBundleDetailed(upload(await get("/bundle")), upload(manifest), output, location);
    if (size < 1) throw Error("Compiler returned " + size + " at [" +
        [0, 1, 2].map(i => compiler.ReadDiagnostic(location, i)) + "]");
    const bytes = Uint8Array.from({ length: size }, (_, i) => compiler.ReadByte(output, i));
    const { instance } = await WebAssembly.instantiate(bytes);
    return execution.calls.map(({ name, input }) => instance.exports[name](input));
}

const get = path => assets.get(path);
const node = { metadata: await inspect(get, tests), execution: await execute(get, execution) };
assert.deepEqual(node.metadata, cases.map(test => test.expected));
assert.deepEqual(node.execution, execution.calls.map(call => call.expected));
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://localhost/**", route => {
        const bytes = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
            : { contentType: "text/html", body: "<title>Generic method metadata</title>" });
    });
    await page.goto("http://localhost/");
    const edge = await page.evaluate(async ({ tests, execution, metadataSource, executionSource }) => {
        const inspect = (0, eval)("(" + metadataSource + ")");
        const execute = (0, eval)("(" + executionSource + ")");
        const get = async path => new Uint8Array(await (await fetch(path, { signal: AbortSignal.timeout(15000) })).arrayBuffer());
        return { metadata: await inspect(get, tests), execution: await execute(get, execution) };
    }, { metadataSource: inspect.toString(), executionSource: execute.toString(), tests, execution });
    assert.deepEqual(edge, node);
    const report = { node: process.version, edge: browser.version(), comparisons: cases.length,
        results: tests.map((test, index) => ({ ...test, values: node.metadata[index] })),
        execution: node.execution };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2));
    console.log("Node and Edge matched " + cases.length + " CLR metadata observations and " +
        execution.calls.length + " generic method results.");
} finally { await browser.close(); }
