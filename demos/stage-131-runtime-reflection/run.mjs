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
const assets = new Map([["/compiler", artifact], ["/resource-image", readFileSync(oracle.fixture)]]);
const tests = oracle.results.map((test, index) => {
    const asset = path => ({ path: basename(path), bytes: readFileSync(path), loadedFromBytes: test.mode === "memory" });
    const [bundle, entries] = packEntry(asset(oracle.fixture), [asset(oracle.dependency)], test.row, true);
    const source = "/bundle-" + index, manifest = "/entries-" + index;
    assets.set(source, bundle);
    assets.set(manifest, entries);
    return { ...test, source, manifest };
});

async function execute({ tests, resourceName, get }) {
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
    const imageBytes = await get("/resource-image");
    const image = upload(imageBytes);
    const encoder = new TextEncoder();
    const found = compiler.FindEmbeddedResource(image, upload(encoder.encode(resourceName)));
    if (found < 0n) throw Error("Embedded resource lookup returned " + found);
    const offset = Number(found & 0xffffffffn), length = Number(found >> 32n);
    if (offset > imageBytes.length || length > imageBytes.length - offset)
        throw Error("Embedded resource range leaves the input image");
    const bytes = new Uint8Array(await crypto.subtle.digest("SHA-256", imageBytes.subarray(offset, offset + length)));
    const digest = Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
    for (const missing of [resourceName.toUpperCase(), resourceName + "\0suffix", ""]) {
        if (compiler.FindEmbeddedResource(image, upload(encoder.encode(missing))) !== -4n)
            throw Error("Raw resource lookup matched an absent ordinal name");
    }
    return { results, resource: { length, digest } };
}

const { resourceName } = oracle;
const node = await execute({ tests, resourceName, get: path => assets.get(path) });
for (const [index, test] of oracle.results.entries()) assert.deepEqual(node.results[index].values, [test.expected]);
for (const { length, digest } of oracle.resourceResults) assert.deepEqual(node.resource, { length, digest });
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://localhost/**", route => {
        const bytes = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
            : { contentType: "text/html", body: "<title>Pending address verification</title>" });
    });
    await page.goto("http://localhost/");
    const edge = await page.evaluate(async ({ tests, resourceName, code }) => {
        const execute = (0, eval)("(" + code + ")");
        return execute({ tests, resourceName, get: async path => new Uint8Array(await (await fetch(path)).arrayBuffer()) });
    }, { tests, resourceName, code: execute.toString() });
    assert.deepEqual(edge, node);
    const result = { node: process.version, edge: browser.version(), ...node,
        compilerSha256: createHash("sha256").update(artifact).digest("hex") };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2));
    console.log("Node and Edge matched " + oracle.results.length +
        " CLR reflection cases and the original embedded resource bytes. Evidence at " + evidence);
} finally {
    await browser.close();
}
