import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos/stage-160-native-receiver-demand");
const evidence = join(root, "artifacts/stage-160-native-receiver-demand");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
const image = readFileSync(oracle.assembly);
const header = Buffer.alloc(4);
header.writeUInt32LE(image.length);
const bundle = Buffer.concat([header, image]);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH ||
    join(root, "artifacts/StandardWasm.Compiler.wasm"));

async function verify(compiler, bundle, cases) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }

    function carrierCount(bytes) {
        let cursor = 8;
        function unsigned() {
            let value = 0, shift = 0, byte;
            do {
                byte = bytes[cursor++];
                value |= (byte & 0x7f) << shift;
                shift += 7;
            } while ((byte & 0x80) !== 0);
            return value;
        }
        while (cursor < bytes.length) {
            const section = bytes[cursor++];
            const size = unsigned();
            if (section !== 10) {
                cursor += size;
                continue;
            }
            let carriers = 0;
            const count = unsigned();
            for (let i = 0; i < count; i++) {
                const length = unsigned();
                if (length === 3 && bytes[cursor] === 0 && bytes[cursor + 1] === 0 && bytes[cursor + 2] === 0x0b)
                    carriers++;
                cursor += length;
            }
            return carriers;
        }
        throw Error("The compiler emitted no Wasm code section");
    }

    const source = upload(bundle);
    const output = tool.CreateBytes(1 << 20);
    const results = [];
    for (const { name, row, hex } of cases) {
        const entry = Uint8Array.of(0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110);
        new DataView(entry.buffer).setUint32(0, row, true);
        const diagnostic = tool.CreateDiagnostic();
        const size = tool.CompileBundleDetailed(source, upload(entry), output, diagnostic);
        if (size <= 0)
            throw Error(name + " compiler diagnostic " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const wasm = Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i));
        const emittedHex = Array.from(wasm, byte => byte.toString(16).padStart(2, "0")).join("");
        if (emittedHex !== hex) throw Error(name + " differs from the native compiler bytes");
        const { instance, module } = await WebAssembly.instantiate(wasm, {
            "standardwasm:resources": { memory: new WebAssembly.Memory({ initial: 1 }) }
        });
        const imports = WebAssembly.Module.imports(module).map(entry => [entry.module, entry.name, entry.kind]);
        if (JSON.stringify(imports) !== '[["standardwasm:resources","memory","memory"]]')
            throw Error(name + " has unexpected host imports");
        results.push({ name, value: instance.exports.run(), carriers: carrierCount(wasm) });
    }
    return results;
}

const node = await verify(compiler, bundle, oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = { "/compiler": compiler, "/bundle": bundle };
    await page.route("http://127.0.0.1/**", route => {
        const asset = assets[new URL(route.request().url()).pathname];
        return asset ? route.fulfill({ body: asset, contentType: "application/octet-stream" })
            : route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, cases }) => {
        const [compiler, bundle] = await Promise.all(["/compiler", "/bundle"].map(async url =>
            new Uint8Array(await (await fetch(url)).arrayBuffer())));
        return (0, eval)("(" + source + ")")(compiler, bundle, cases);
    }, { source: verify.toString(), cases: oracle.cases });
    assert.deepEqual(edge, node);
    assert.deepEqual(node, oracle.cases.map(({ name, value, carriers }) => ({ name, value, carriers })));
    const results = {
        clr: oracle.clr, architecture: oracle.architecture, node: process.version, edge: browser.version(),
        compiler: createHash("sha256").update(compiler).digest("hex"), outcomes: node
    };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2) + "\n");
    console.log(JSON.stringify(results));
} finally {
    await browser.close();
}
