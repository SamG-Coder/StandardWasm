import assert from "node:assert/strict";
import { createServer } from "node:http";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos", "stage-146-class-value-receivers");
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const output = join(root, "artifacts", "class-value-receivers");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));
const runtime = wasm32Runtime();
const oraclePath = join(evidence, "oracle.json");
const fixturePath = join(stage, "bin", "Release", "net10.0", "Fixture.dll");
runOn32BitClr(runtime, stage, fixturePath, [oraclePath]);
const { architecture, results: oracle } = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(architecture, "X86");
const fixture = readFileSync(fixturePath);
const framework = ["System.Collections.Specialized.dll", "System.Runtime.dll", "System.Private.CoreLib.dll"]
    .map(path => ({ path, bytes: readFileSync(join(runtime.directory, path)) }));
const [bundle] = packEntry({ path: "Fixture.dll", bytes: fixture }, framework, oracle[0].row, true);
const assets = [compiler, bundle];

async function run(compiler, bundle, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const data = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(data, i, bytes[i]);
        return data;
    }
    const image = upload(new Uint8Array(bundle));
    const output = build.CreateBytes(1 << 20), location = build.CreateDiagnostic();
    const methods = new Map();
    const results = [];
    for (const { method, row, arguments: args } of cases) {
        if (!methods.has(row)) {
            const entry = new Uint8Array(11);
            new DataView(entry.buffer).setUint32(0, row, true);
            entry.set([3, 0, 0, 0, 114, 117, 110], 4);
            const size = build.CompileBundleDetailed(image, upload(entry), output, location);
            if (size <= 0 || size > (1 << 20))
                throw Error(`${method} returned ${size} at ${[0, 1, 2].map(i => build.ReadDiagnostic(location, i))}`);
            const code = Uint8Array.from({ length: size }, (_, i) => build.ReadByte(output, i));
            const { instance } = await WebAssembly.instantiate(code);
            methods.set(row, instance.exports.run);
        }
        results.push({ method, result: methods.get(row)(...args) });
    }
    return results;
}
const node = await run(...assets, oracle);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const server = createServer((req, res) => {
        if (req.url === "/") return res.end("<!doctype html><title>Stage 146 CLR comparison</title>");
        const body = assets[Number(req.url.slice(1))];
        if (!body) {
            res.writeHead(404);
            return res.end();
        }
        res.writeHead(200, { "Content-Type": "application/octet-stream", "Content-Length": body.length });
        res.end(body);
    });
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    let edge;
    try {
        await page.goto(`http://127.0.0.1:${server.address().port}/`);
        edge = await page.evaluate(async ({ cases, source }) => {
            const assets = await Promise.all([0, 1].map(async i => (await fetch("/" + i)).arrayBuffer()));
            return (0, eval)("(" + source + ")")(...assets, cases);
        }, { cases: oracle, source: run.toString() });
    } finally {
        await new Promise(resolve => server.close(resolve));
    }
    assert.deepEqual(edge, node);
    assert.deepEqual(node.map(({ result }) => result), oracle.map(({ value }) => value));
    const report = { node: process.version, edge: browser.version(), clr: runtime.version, comparisons: node.length, cases: node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2));
    console.log(`Node and Edge matched ${node.length} class value-field receiver cases from x86 CLR ${runtime.version}.`);
} finally {
    await browser.close();
}
