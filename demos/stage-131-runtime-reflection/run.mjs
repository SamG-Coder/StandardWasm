import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { chromium } from "playwright-core";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";

import { basename, join } from "node:path";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-131-runtime-reflection");
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compilerPath)) throw Error("Build the compiler with npm test first.");
const base = join(root, "artifacts", "runtime-reflection");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const artifact = readFileSync(compilerPath);

run("dotnet", ["build", join(stage, "Fixture"), "-c", "Release"]);
const built = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built, "ReflectionFixture.dll"), join(built, "ReflectionDependency.dll"), evidence]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
const assets = new Map([["/compiler", artifact]]);
const tests = oracle.results.map((test, index) => {
    const asset = path => ({ path: basename(path), bytes: readFileSync(path), loadedFromBytes: test.mode === "memory" });
    const [bundle, entries] = packEntry(asset(oracle.fixture), [asset(oracle.dependency)], test.row, true);
    const source = "/bundle-" + index, manifest = "/entries-" + index;
    assets.set(source, bundle);
    assets.set(manifest, entries);
    return { ...test, source, manifest };
});

async function execute({ tests, get }) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(await get("/compiler"));
    function upload(bytes) {
        const array = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(array, i, bytes[i]);
        return array;
    }
    const results = [];
    for (const test of tests) {
        const buffer = compiler.CreateBytes(65536);
        const location = compiler.CreateDiagnostic();
        const size = compiler.CompileBundleDetailed(upload(await get(test.source)), upload(await get(test.manifest)), buffer, location);
        if (size < 1) {
            const diagnostic = [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index));
            throw Error(test.method + " returned " + size + " [" + diagnostic + "]");

        }

        const code = Uint8Array.from({ length: size }, (_, index) => compiler.ReadByte(buffer, index));
        const { instance } = await WebAssembly.instantiate(code);
        const values = [instance.exports.run()];
        results.push({ method: test.method, size, values });
    }
    return results;
}

const node = await execute({ tests, get: path => assets.get(path) });
for (const [index, test] of oracle.results.entries()) assert.deepEqual(node[index].values, [test.expected]);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://reflection-accessors.test/**", route => {
        const bytes = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
            : { contentType: "text/html", body: "<title>Pending address verification</title>" });
    });
    await page.goto("http://reflection-accessors.test/");
    const edge = await page.evaluate(async ({ tests, code }) => {
        const execute = (0, eval)("(" + code + ")");
        return execute({ tests, get: async path => new Uint8Array(await (await fetch(path)).arrayBuffer()) });
    }, { tests, code: execute.toString() });
    assert.deepEqual(edge, node);
    const result = { node: process.version, edge: browser.version(), results: node,
        compilerSha256: createHash("sha256").update(artifact).digest("hex") };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2));
    console.log("Node and Edge matched " + oracle.results.length + " CLR reflection cases. Evidence at " + evidence);
} finally {
    await browser.close();
}