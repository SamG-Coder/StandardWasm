import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos/stage-206-uint32-managed-byrefs");
const evidence = join(root, "artifacts/stage-206-uint32-managed-byrefs");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracleText = readFileSync(oraclePath, "utf8");
const oracle = JSON.parse(oracleText);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const fixture = readFileSync(join(stage, "Fixture/bin/Release/netstandard2.0/UInt32ByrefFixture.dll"));

async function verify(compiler, fixture, cases) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const image = tool.CreateBytes(fixture.length);
    fixture.forEach((value, index) => tool.SetByte(image, index, value));
    const output = tool.CreateBytes(256 * 1024);
    const location = tool.CreateDiagnostic();
    const outcomes = [];
    for (const test of cases) {
        const size = tool.CompileDetailed(image, test.row, output, location);
        if (size <= 0) {
            const diagnostic = [0, 1, 2].map(index => tool.ReadDiagnostic(location, index));
            throw Error(test.name + " returned " + size + " at " + diagnostic);
        }
        const bytes = Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index));
        const { instance: { exports: { run } } } = await WebAssembly.instantiate(bytes);
        for (const { input, result } of test.values) {
            const actual = run(input) >>> 0;
            if (actual !== result) throw Error(test.name + " differs from CLR for " + input);
            outcomes.push({ name: test.name, input, result: actual });
        }
    }
    return outcomes;
}

const node = await verify(compiler, fixture, oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = new Map([
        ["/", ["text/html", "<!doctype html>"]],
        ["/compiler", ["application/wasm", compiler]],
        ["/fixture", ["application/octet-stream", fixture]],
        ["/oracle", ["application/json", oracleText]]
    ]);
    await page.route("http://standardwasm.test/**", route => {
        const [contentType, body] = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill({ contentType, body });
    });
    await page.goto("http://standardwasm.test/");
    const edge = await page.evaluate(async verifier => {
        const run = (0, eval)("(" + verifier + ")");
        const [compiler, fixture, oracle] = await Promise.all([
            fetch("/compiler").then(response => response.arrayBuffer()),
            fetch("/fixture").then(response => response.arrayBuffer()),
            fetch("/oracle").then(response => response.json())
        ]);
        return run(compiler, new Uint8Array(fixture), oracle.cases);
    }, verify.toString());
    assert.deepEqual(edge, node);
    const result = { clr: oracle.clr, node: process.version, edge: browser.version(),
        compilerHash: createHash("sha256").update(compiler).digest("hex"), outcomes: node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log(node.length + " CLR UInt32 managed-location cases passed in Node and Edge.");
} finally {
    await browser.close();
}
