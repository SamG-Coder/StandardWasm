import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-239-checked-native-integer-construction");
const evidence = join(root, "artifacts/stage-239-checked-native-integer-construction");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/NativeIntegerConstruction.dll"));

async function verify({ compiler, image, cases }) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    const encoder = new TextEncoder();
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const input = upload(decode(image)), output = tool.CreateBytes(1 << 18), diagnostic = tool.CreateDiagnostic();
    const exports = new Map();
    for (const name of new Set(cases.map(test => test.name))) {
        const row = tool.FindRow(input, upload(encoder.encode(name)));
        if (row < 1) throw Error("Missing fixture export " + name);
        const size = tool.CompileDetailed(input, row, output, diagnostic);
        if (size < 1) throw Error(name + " status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
        if (WebAssembly.Module.imports(module).length !== 0) throw Error("Unexpected host dependency for " + name);
        exports.set(name, new WebAssembly.Instance(module).exports);
    }
    return cases.map(({ name, input, wide, unsignedResult }) => {
        const guest = exports.get(name);
        try {
            const value = guest.run(wide ? BigInt(input) : Number(input));
            return unsignedResult ? value >>> 0 : value;
        } catch (error) {
            const tag = guest["\0overflow"];
            if (error instanceof WebAssembly.Exception && tag instanceof WebAssembly.Tag && error.is(tag))
                return "System.OverflowException";
            throw error;
        }
    });
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64"), cases: oracle.cases };
const node = await verify(input);
assert.deepEqual(node, oracle.cases.map(test => test.expected));
const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(({ input, source }) => (0, eval)("(" + source + ")")(input), { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(image), clr: oracle.clr, oracle: oracle.cases,
        nodeVersion: process.version, edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 CLR on " + oracle.cases.length + " native integer constructor cases.");
} finally {
    await browser?.close();
    await server.close();
}
