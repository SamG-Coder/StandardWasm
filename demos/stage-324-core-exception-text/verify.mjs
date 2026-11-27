import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-324-core-exception-text");
const evidence = join(root, "artifacts/stage-324-core-exception-text");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const core = readFileSync(oracle.coreLib);
const witness = readFileSync(oracle.witness);

async function exercise(compiler, core, witness, row, inputs) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const encoder = new TextEncoder();
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    async function compile(source, entries) {
        const manifest = [];
        for (const [name, row] of entries) {
            if (row < 1) throw Error("Missing managed method " + name);
            const bytes = encoder.encode(name);
            const record = new Uint8Array(8 + bytes.length);
            const view = new DataView(record.buffer);
            view.setUint32(0, row, true);
            view.setUint32(4, bytes.length, true);
            record.set(bytes, 8);
            manifest.push(...record);
        }
        const output = tool.CreateBytes(1 << 20), diagnostic = tool.CreateDiagnostic();
        const size = tool.CompileModuleDetailed(source, upload(manifest), output, diagnostic);
        if (size < 1) throw Error("Compile status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const emitted = Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index));
        return (await WebAssembly.instantiate(emitted)).instance.exports;
    }
    const native = await compile(upload(core), [["Lookup", row]]);
    const source = upload(witness);
    const reader = await compile(source, ["Length", "Character"].map(name => [name, tool.FindRow(source, upload(encoder.encode(name)))]));
    const emptyReference = native.Lookup(0);
    return inputs.map(input => {
        const first = native.Lookup(input), second = native.Lookup(input);
        let text = "";
        const length = reader.Length(first);
        for (let index = 0; index < length; index++) text += String.fromCharCode(reader.Character(first, index));
        return { input, text, same: first === second, empty: first === emptyReference };
    });
}

const inputs = oracle.cases.map(item => item.input);
const node = await exercise(compiler, core, witness, oracle.row, inputs);
assert.deepEqual(node, oracle.cases);
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/exception-corelib.dll", route => route.fulfill({ body: core, contentType: "application/octet-stream" }));
    await page.route("**/exception-witness.dll", route => route.fulfill({ body: witness, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, row, inputs }) => {
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/exception-corelib.dll", "/exception-witness.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + source + ")")(...assets, row, inputs);
    }, { source: exercise.toString(), row: oracle.row, inputs });
    assert.deepEqual(edge, node);
    const digest = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: digest(compiler), core: digest(core), witness: digest(witness),
        clr: oracle.clr, node: process.version, edge: browser.version(), cases: node
    }, null, 2) + "\n");
    console.log("Native exception text and string identity matched Windows x86, Node, and Edge in " + node.length + " cases.");
} finally {
    await browser.close();
    await server.close();
}
