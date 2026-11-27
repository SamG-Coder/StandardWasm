import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-304-textinfo-ascii-case");
const evidence = join(root, "artifacts", "stage-304-textinfo-ascii-case");
mkdirSync(evidence, { recursive: true });
const runtime = wasm32Runtime();
const mono = process.env.STANDARDWASM_MONO_CORELIB || join(process.env.USERPROFILE, ".nuget", "packages",
    "microsoft.netcore.app.runtime.mono.browser-wasm", runtime.version, "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(runtime, join(stage, "Oracle", "Oracle.csproj"),
    join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"), [oraclePath, mono]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm"));
const core = readFileSync(mono);
const fixture = readFileSync(oracle.assembly);
const expected = oracle.report.trimEnd().split("\n");

async function verify(compiler, core, fixture, rows, count) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(array, i, bytes[i]);
        return array;
    }
    const output = tool.CreateBytes(2 << 20);
    const diagnostic = tool.CreateDiagnostic();
    async function compile(image, entries) {
        const manifest = Uint8Array.from(entries.flatMap(([row, name]) => [
            row & 255, (row >> 8) & 255, (row >> 16) & 255, 0, name.length, 0, 0, 0,
            ...Array.from(name, character => character.charCodeAt(0))
        ]));
        const length = tool.CompileModuleDetailed(upload(new Uint8Array(image)), upload(manifest), output, diagnostic);
        if (length <= 0) throw Error("ASCII case compile " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const bytes = Uint8Array.from({ length }, (_, index) => tool.ReadByte(output, index));
        const { instance } = await WebAssembly.instantiate(bytes);
        return instance.exports;
    }

    const data = await compile(fixture, [[rows.input, "input"], [rows.codeUnit, "codeUnit"]]);
    const text = await compile(core, [[rows.lower, "lower"]]);
    const outcomes = [];
    const units = value => Array.from({ length: data.codeUnit(value, -1) }, (_, index) => data.codeUnit(value, index));
    for (let index = 0; index < count; index++) {
        const source = data.input(index);
        const line = ["ASCII", index];
        if (source === null) {
            try {
                text.lower(source);
                throw Error("Expected typed null reference");
            } catch (error) {
                if (!(error instanceof WebAssembly.Exception) || !error.is(text["\0nullReference"])) throw error;
                outcomes.push([...line, "NullReferenceException"].join("|"));
            }
            continue;
        }
        const before = units(source);
        const lowered = text.lower(source);
        const again = text.lower(source);
        const after = units(source);
        if (after.length !== before.length || after.some((unit, offset) => unit !== before[offset]))
            throw Error("ASCII transform changed its original input");
        // The fixture and linked CoreLib are separate Wasm instantiations.
        // Each owns String.Empty, so the empty check compares two CoreLib calls.
        const same = before.length === 0 ? lowered === again : lowered === source;
        if (!same && lowered === again) throw Error("Expected fresh case-normalized output");
        outcomes.push([...line, same ? "same" : "fresh", ...units(lowered)].join("|"));
    }
    return outcomes;
}

const node = await verify(compiler, core, fixture, oracle.rows, expected.length);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = { "/compiler": compiler, "/core": core, "/fixture": fixture };
    await page.route("http://127.0.0.1/**", route => {
        const body = assets[new URL(route.request().url()).pathname];
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, rows, count }) => {
        const inputs = await Promise.all(["/compiler", "/core", "/fixture"].map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, rows, count);
    }, { source: verify.toString(), rows: oracle.rows, count: expected.length });
    assert.deepEqual(edge, node);
    assert.deepEqual(node, expected);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), core: hash(core), assembly: hash(fixture), node: process.version,
        edge: browser.version(), clr: oracle.runtime, outcomes: node
    }, null, 2) + "\n");
    console.log("Stage 304 ASCII normalization passed", node.length, "CLR/Node/Edge cases.");
} finally {
    await browser.close();
}
