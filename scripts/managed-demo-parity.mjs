import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "./runtime-packs.mjs";
import { root } from "./toolchain.mjs";

export async function verifyManagedDemo(stageName) {
    const stage = join(root, "demos", stageName);
    const evidence = join(root, "artifacts", stageName);
    mkdirSync(evidence, { recursive: true });
    const oraclePath = join(evidence, "oracle.json");
    runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
        join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
    const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
    const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
    const assembly = readFileSync(oracle.assembly);
    const images = [assembly, ...(oracle.dependencies ?? []).map(path => readFileSync(path))];
    const bundle = Buffer.concat(images.flatMap(image => {
        const header = Buffer.alloc(4);
        header.writeUInt32LE(image.length);
        return [header, image];
    }));

    async function verify(compiler, bundle, cases) {
        const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
        function upload(bytes) {
            const array = tool.CreateBytes(bytes.length);
            for (let i = 0; i < bytes.length; i++) tool.SetByte(array, i, bytes[i]);
            return array;
        }
        const input = upload(new Uint8Array(bundle));
        const output = tool.CreateBytes(2 << 20);
        const diagnostic = tool.CreateDiagnostic();
        const outcomes = [];
        for (const { name, row, expected } of cases) {
            const manifest = Uint8Array.from([0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110]);
            new DataView(manifest.buffer).setUint32(0, row, true);
            const length = tool.CompileBundleDetailed(input, upload(manifest), output, diagnostic);
            if (length <= 0) {
                const location = [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i));
                outcomes.push({ name, expected, status: length, location });
                continue;
            }
            const bytes = Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i));
            const module = await WebAssembly.compile(bytes);
            if (WebAssembly.Module.imports(module).length !== 0) throw Error(name + " requires host imports");
            const instance = await WebAssembly.instantiate(module);
            outcomes.push({ name, expected, result: instance.exports.run() });
        }
        return outcomes;
    }

    const node = await verify(compiler, bundle, oracle.cases);
    const browser = await chromium.launch({ channel: "msedge" });
    try {
        const page = await browser.newPage();
        const assets = { "/compiler": compiler, "/bundle": bundle };
        await page.route("http://127.0.0.1/**", route => {
            const asset = assets[new URL(route.request().url()).pathname];
            return asset ? route.fulfill({ body: asset, contentType: "application/octet-stream" }) :
                route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
        });
        await page.goto("http://127.0.0.1/");
        const edge = await page.evaluate(async ({ source, cases }) => {
            const inputs = await Promise.all(["/compiler", "/bundle"].map(async path =>
                (await fetch(path)).arrayBuffer()));
            return (0, eval)("(" + source + ")")(...inputs, cases);
        }, { source: verify.toString(), cases: oracle.cases });
        const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
        const result = { compiler: sha256(compiler), assembly: sha256(assembly), clr: oracle.runtime,
            node: process.version, edge: browser.version(), outcomes: node };
        writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
        assert.deepEqual(edge, node);
        for (const [index, test] of node.entries()) {
            const rejection = oracle.cases[index].rejection;
            if (rejection) {
                assert.equal(test.status, rejection.status, JSON.stringify(test));
                assert.deepEqual(test.location, rejection.location, JSON.stringify(test));
            } else {
                assert.equal(test.result, test.expected, JSON.stringify(test));
            }
        }
        console.log(JSON.stringify(result));
    } finally {
        await browser.close();
    }
}
