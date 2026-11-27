import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-317-readonly-ordinal-string-borrow");
const evidence = join(root, "artifacts", "stage-317-readonly-ordinal-string-borrow");
mkdirSync(evidence, { recursive: true });
const runtime = wasm32Runtime();
const mono = process.env.STANDARDWASM_MONO_CORELIB || join(process.env.USERPROFILE, ".nuget", "packages",
    "microsoft.netcore.app.runtime.mono.browser-wasm", runtime.version, "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
const oraclePath = join(evidence, "oracle.json");
const borrowPath = join(evidence, "borrow.wasm");
runOn32BitClr(runtime, join(stage, "Oracle", "Oracle.csproj"),
    join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"), [oraclePath, borrowPath, mono]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));

const assets = {
    "/compiler": readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm")),
    "/fixture": readFileSync(oracle.assembly),
    "/borrow": readFileSync(borrowPath)
};

async function verify(compiler, fixture, borrow, row) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(array, index, bytes[index]);
        return array;
    }

    const output = tool.CreateBytes(2 << 20);
    const diagnostic = tool.CreateDiagnostic();
    const entries = Uint8Array.of(row & 255, (row >> 8) & 255, (row >> 16) & 255, 0, 5, 0, 0, 0,
        105, 110, 112, 117, 116);
    const length = tool.CompileModuleDetailed(upload(new Uint8Array(fixture)), upload(entries), output, diagnostic);
    if (length <= 0) throw Error("String fixture compile " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const compiled = Uint8Array.from({ length }, (_, index) => tool.ReadByte(output, index));
    const { instance: { exports: data } } = await WebAssembly.instantiate(compiled);
    const { instance: { exports: probe } } = await WebAssembly.instantiate(borrow);
    const outcomes = [];
    for (let index = 0; index < 5; index++) {
        try {
            outcomes.push(probe.borrow(data.input(index), index < 3 ? 1 : 0));
        } catch (error) {
            if (!(error instanceof WebAssembly.Exception) || !error.is(probe["\0nullReference"])) throw error;
            outcomes.push(-1);
        }
    }
    return outcomes;
}

const node = await verify(...Object.values(assets), oracle.row);
assert.deepEqual(node, oracle.outcomes);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const body = assets[new URL(route.request().url()).pathname];
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, paths, row }) => {
        const inputs = await Promise.all(paths.map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, row);
    }, { source: verify.toString(), paths: Object.keys(assets), row: oracle.row });
    assert.deepEqual(edge, node);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(assets["/compiler"]), fixture: hash(assets["/fixture"]), borrow: hash(assets["/borrow"]),
        node: process.version, edge: browser.version(), clr: oracle.runtime, profiles: oracle.profiles, outcomes: node
    }, null, 2) + "\n");
    console.log("Stage 317 ordinal string borrow passed", node.length, "x86 CLR/Node/Edge cases.");
} finally {
    await browser.close();
}
