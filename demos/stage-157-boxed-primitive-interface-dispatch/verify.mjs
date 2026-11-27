import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos/stage-157-boxed-primitive-interface-dispatch");
const evidence = join(root, "artifacts/stage-157-boxed-primitive-interface-dispatch");
mkdirSync(evidence, { recursive: true });
run("dotnet", ["build", join(stage, "BoxedPrimitiveInterfaces.csproj"), "-c", "Release", "--nologo"]);
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), stage, join(stage, "bin/Release/net10.0/BoxedPrimitiveInterfaces.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm"));
const warmedBundle = Buffer.concat(oracle.files.flatMap(file => {
    const bytes = readFileSync(file);
    const header = Buffer.alloc(4);
    header.writeUInt32LE(bytes.length);
    return [header, bytes];
}));
async function verify(compiler, bytes, cases) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const result = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(result, i, bytes[i]);
        return result;
    }
    const bundle = upload(new Uint8Array(bytes));
    const output = tool.CreateBytes(2 << 20);
    const diagnostic = tool.CreateDiagnostic();
    const results = [];
    for (const test of cases) {
        const manifest = new Uint8Array(11);
        new DataView(manifest.buffer).setUint32(0, test.row, true);
        manifest.set([3, 0, 0, 0, 114, 117, 110], 4);
        const status = tool.CompileBundleDetailed(bundle, upload(manifest), output, diagnostic);
        if (status < 1) {
            results.push({ name: test.name, expected: test.expected, status,
                diagnostic: [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)) });
            continue;
        }
        const wasm = Uint8Array.from({ length: status }, (_, i) => tool.ReadByte(output, i));
        const { instance } = await WebAssembly.instantiate(wasm);
        try {
            const result = instance.exports.run();
            results.push({ name: test.name, expected: test.expected, result });
        } catch (error) {
            if (!(error instanceof WebAssembly.RuntimeError) && !(error instanceof WebAssembly.Exception)) throw error;
            results.push({ name: test.name, expected: test.expected, failure: error.constructor.name });
        }
    }
    return results;
}

const node = await verify(compiler, warmedBundle, oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = { "/compiler": compiler, "/bundle": warmedBundle };
    await page.route("http://127.0.0.1/**", route => {
        const asset = assets[new URL(route.request().url()).pathname];
        return asset ? route.fulfill({ body: asset, contentType: "application/octet-stream" })
            : route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, cases }) => {
        const [compiler, bundle] = await Promise.all(["/compiler", "/bundle"].map(async path =>
            (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, bundle, cases);
    }, { source: verify.toString(), cases: oracle.cases });
    assert.deepEqual(edge, node);
    const result = { clr: oracle.runtime, node: process.version, edge: browser.version(), outcomes: node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    for (let index = 0; index < node.length; index++) {
        const actual = node[index];
        const { expected, acceptedDiagnostic } = oracle.cases[index];
        if (actual.status === -2 && acceptedDiagnostic) {
            assert.deepEqual(actual.diagnostic, acceptedDiagnostic, actual.name);
        } else {
            assert.equal(actual.result, expected, actual.name);
        }
    }
    console.log(JSON.stringify(result));
} finally {
    await browser.close();
}
