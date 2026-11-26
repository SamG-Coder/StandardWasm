import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos", "stage-136-method-instantiations");
const base = join(root, "artifacts", "method-instantiations");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const oracle = join(evidence, "oracle.json"), reader = join(evidence, "Reader.wasm");

for (const project of [join(stage, "Fixture"), join(stage, "Reader"), join(root, "tools", "PortableCompiler.Driver")])
    run("dotnet", ["build", project, "-c", "Release"]);
const driver = join(root, "tools", "PortableCompiler.Driver", "bin", "Release", "net10.0", "PortableCompiler.Driver.dll");
const built = project => join(stage, project, "bin", "Release", "netstandard2.0");
run("dotnet", [driver, join(built("Reader"), "Reader.dll"), "MethodSpecProbe", reader]);

const runtime = wasm32Runtime();
const core = "System.Private.CoreLib.dll";
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built("Fixture"), "MethodSpecFixture.dll"), join(runtime.directory, core), join(runtime.widePack, core), oracle]);
const cases = JSON.parse(readFileSync(oracle, "utf8"));
const assets = new Map([["/reader", readFileSync(reader)]]);
for (const { source, path } of cases)
    if (!assets.has("/" + source)) assets.set("/" + source, readFileSync(path));
const tests = cases.map(({ source, row }) => ({ source, row }));

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

const node = await inspect(path => assets.get(path), tests);
assert.deepEqual(node, cases.map(test => test.expected));
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://localhost/**", route => {
        const bytes = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
            : { contentType: "text/html", body: "<title>Generic method metadata</title>" });
    });
    await page.goto("http://localhost/");
    const edge = await page.evaluate(async ({ tests, code }) => {
        const run = (0, eval)("(" + code + ")");
        return run(async path => new Uint8Array(await (await fetch(path, { signal: AbortSignal.timeout(15000) })).arrayBuffer()), tests);
    }, { code: inspect.toString(), tests });
    assert.deepEqual(edge, node);
    const report = { node: process.version, edge: browser.version(), comparisons: cases.length,
        results: tests.map((test, index) => ({ ...test, values: node[index] })) };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2));
    console.log("Node and Edge matched " + cases.length + " CLR metadata observations.");
} finally { await browser.close(); }
