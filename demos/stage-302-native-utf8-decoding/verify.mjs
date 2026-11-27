import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-302-native-utf8-decoding");
const evidence = join(root, "artifacts", "stage-302-native-utf8-decoding");
mkdirSync(evidence, { recursive: true });
const runtime = wasm32Runtime();
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(runtime, join(stage, "Oracle", "Oracle.csproj"),
    join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const mono = process.env.STANDARDWASM_MONO_CORELIB || join(process.env.USERPROFILE, ".nuget", "packages",
    "microsoft.netcore.app.runtime.mono.browser-wasm", runtime.version, "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm"));
const fixture = readFileSync(oracle.assembly);
const bundledImages = [fixture, ...["System.Runtime.dll", "System.Runtime.InteropServices.dll"].map(name =>
    readFileSync(join(runtime.directory, name))), readFileSync(mono)];
const bundle = Buffer.concat(bundledImages.flatMap(image => {
    const size = Buffer.alloc(4);
    size.writeUInt32LE(image.length);
    return [size, image];
}));

async function verify(compiler, bundle, row, cases) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const buffer = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(buffer, index, bytes[index]);
        return buffer;
    }
    const manifest = Uint8Array.of(0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110);
    new DataView(manifest.buffer).setUint32(0, row, true);
    const output = tool.CreateBytes(2 << 20);
    const located = tool.CreateDiagnostic();
    const length = tool.CompileBundleDetailed(upload(new Uint8Array(bundle)), upload(manifest), output, located);
    if (length <= 0) throw Error("UTF-8 compile " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(located, i)));
    const bytes = Uint8Array.from({ length }, (_, index) => tool.ReadByte(output, index));
    const module = await WebAssembly.compile(bytes);
    const declared = WebAssembly.Module.imports(module);
    if (declared.length !== 1 || declared[0].module !== "standardwasm:resources" ||
        declared[0].name !== "memory" || declared[0].kind !== "memory") throw Error("Unexpected UTF-8 import");
    const memory = new WebAssembly.Memory({ initial: 1 });
    const { exports: { run } } = await WebAssembly.instantiate(module, { "standardwasm:resources": { memory } });
    const memoryBytes = new Uint8Array(memory.buffer);
    const outcomes = cases.map(({ name, input }) => {
        const address = input.length ? memoryBytes.length - input.length : 0;
        if (address) memoryBytes.set(input, address);
        const count = run(address, -1);
        const units = count < 0 ? [-1] : Array.from({ length: count }, (_, index) => run(address, index));
        return { name, units };
    });
    memoryBytes[memoryBytes.length - 1] = 0x41;
    let unterminatedTrapped = false;
    try {
        run(memoryBytes.length - 1, -1);
    } catch (error) {
        if (!(error instanceof WebAssembly.RuntimeError)) throw error;
        unterminatedTrapped = true;
    }
    return { outcomes, unterminatedTrapped };
}

const node = await verify(compiler, bundle, oracle.row, oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = { "/compiler": compiler, "/bundle": bundle };
    await page.route("http://127.0.0.1/**", route => {
        const body = assets[new URL(route.request().url()).pathname];
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, row, cases }) => {
        const images = await Promise.all(["/compiler", "/bundle"].map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...images, row, cases);
    }, { source: verify.toString(), row: oracle.row, cases: oracle.cases });
    assert.deepEqual(edge, node);
    assert.equal(node.unterminatedTrapped, true);
    assert.deepEqual(node.outcomes, oracle.cases.map(({ name, units }) => ({ name, units })));
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), assembly: hash(fixture), node: process.version, edge: browser.version(),
        clr: oracle.runtime, ...node
    }, null, 2) + "\n");
    console.log("Stage 302 UTF-8 parity passed", node.outcomes.length, "CLR/Node/Edge cases.");
} finally {
    await browser.close();
}
