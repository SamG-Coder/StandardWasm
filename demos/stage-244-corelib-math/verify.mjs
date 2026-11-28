import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const name = "stage-244-corelib-math";
const stage = join(root, "demos", name), evidence = join(root, "artifacts", name);
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const fixture = readFileSync(oracle.fixture), core = readFileSync(oracle.core);
const row = [oracle.fixtureRow, oracle.coreRow];

async function verify(compiler, images, rows, cases) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(target, index, bytes[index]);
        return target;
    }
    const output = tool.CreateBytes(2 << 20), diagnostic = tool.CreateDiagnostic();
    const bits = new DataView(new ArrayBuffer(8));
    const results = [];
    for (let index = 0; index < images.length; index++) {
        const input = upload(new Uint8Array(images[index]));
        const size = tool.CompileDetailed(input, rows[index], output, diagnostic);
        if (size <= 0) throw Error("Math status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const module = await WebAssembly.compile(Uint8Array.from({length: size}, (_, i) => tool.ReadByte(output, i)));
        if (WebAssembly.Module.imports(module).length !== 0) throw Error("Math requires host imports");
        const { exports } = await WebAssembly.instantiate(module);
        results.push(cases.map(test => {
            bits.setBigUint64(0, BigInt("0x" + test.input));
            const value = exports.run(bits.getFloat64(0));
            if (Number.isNaN(value)) return "nan";
            bits.setFloat64(0, value);
            return bits.getBigUint64(0).toString(16).padStart(16, "0");
        }));
    }
    return results;
}

const expected = oracle.cases.map(test => test.expected);
const node = await verify(compiler, [fixture, core], row, oracle.cases);
assert.deepEqual(node, [expected, expected]);
const assets = { "/compiler": compiler, "/fixture": fixture, "/core": core };
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const asset = assets[new URL(route.request().url()).pathname];
        return asset ? route.fulfill({body: asset, contentType: "application/octet-stream"}) :
            route.fulfill({body: "<!doctype html>", contentType: "text/html"});
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, row, cases }) => {
        const input = await Promise.all(["/compiler", "/fixture", "/core"].map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(input[0], input.slice(1), row, cases);
    }, { source: verify.toString(), row, cases: oracle.cases });
    assert.deepEqual(edge, node);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    const result = {compiler: hash(compiler), fixture: hash(fixture), core: hash(core), clr: oracle.clr,
        nodeVersion: process.version, edgeVersion: browser.version(), row, cases: oracle.cases, node, edge};
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log("Both engines matched " + expected.length + " CLR cases through each of the two Math entry paths.");
} finally {
    await browser.close();
}
