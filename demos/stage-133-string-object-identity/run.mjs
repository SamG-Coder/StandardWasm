import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-133-string-object-identity");
const artifact = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const evidenceRoot = join(root, "artifacts", "string-object-identity");
mkdirSync(evidenceRoot, { recursive: true });
const evidence = mkdtempSync(join(evidenceRoot, "run-"));
run("dotnet", ["build", join(stage, "Fixture"), "-c", "Release"]);
const built = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
const path = "StringIdentityFixture.dll", dependency = "StringIdentityDependency.dll";
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built, path), join(built, dependency), oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const files = name => ({ path: name, bytes: readFileSync(join(built, name)) });
const [bundle] = packEntry(files(path), [files(dependency)], oracle.entries[0].row);
const assets = new Map([["/compiler", readFileSync(artifact)], ["/bundle", bundle]]);

async function execute(entries, get) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(await get("/compiler"));
    const encoder = new TextEncoder();
    const manifest = new Uint8Array(entries.reduce((size, { name }) => size + 8 + encoder.encode(name).length, 0));
    const view = new DataView(manifest.buffer);
    let offset = 0;
    for (const { name, row } of entries) {
        const exported = encoder.encode(name);
        view.setUint32(offset, row, true);
        view.setUint32(offset + 4, exported.length, true);
        manifest.set(exported, offset + 8);
        offset += 8 + exported.length;
    }
    function upload(bytes) {
        const input = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(input, i, bytes[i]);
        return input;
    }
    const buffer = compiler.CreateBytes(65536), location = compiler.CreateDiagnostic();
    const size = compiler.CompileBundleDetailed(upload(await get("/bundle")), upload(manifest), buffer, location);
    if (size < 1) throw Error("Compiler returned " + size + " at " + [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index)));
    const code = Uint8Array.from({ length: size }, (_, index) => compiler.ReadByte(buffer, index));
    const { instance } = await WebAssembly.instantiate(code);
    const values = entries.map(({ name }) => instance.exports[name]());
    const repeated = entries.map(({ name }) => instance.exports[name]());
    return {
        identities: values.map(left => repeated.map(right => left === right)),
        nulls: values.map(value => value === null)
    };
}

const node = await execute(oracle.entries, url => assets.get(url));
assert.deepEqual(node, { identities: oracle.identities, nulls: oracle.nulls });
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://localhost/**", route => {
        const bytes = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
            : { contentType: "text/html", body: "<title>String object identity</title>" });
    });
    await page.goto("http://localhost/");
    const edge = await page.evaluate(async ({ entries, code }) => {
        const run = (0, eval)("(" + code + ")");
        return run(entries, async path => new Uint8Array(await (await fetch(path, { signal: AbortSignal.timeout(15000) })).arrayBuffer()));
    }, { entries: oracle.entries, code: execute.toString() });
    assert.deepEqual(edge, node);
    const results = { node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2));
    console.log("Node and Edge matched " + oracle.entries.length ** 2 + " CLR string reference comparisons.");
} finally {
    await browser.close();
}
