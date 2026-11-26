import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos", "stage-107-framework-facades");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "framework-facades");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));
run("dotnet", ["build", "-c", "Release", join(stage, "Consumer")]);
const consumer = join(stage, "Consumer", "bin", "Release");
const assemblies = ["netstandard2.0", "netstandard2.1"].map(target => join(consumer, target, "FacadeConsumer.dll"));
const pack = process.env.STANDARDWASM_RUNTIME_PACK;
// Double.IsNaN and the List constructor are CoreLib bodies, so the default
// pack is the x86 CLR's own 32-bit runtime directory (decision 0044).
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, ...assemblies, ...(pack ? [pack] : [])]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 65536;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();
    const bundles = new Map();

    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }

    function bundle(ids) {
        const key = ids.join(",");
        if (bundles.has(key)) return bundles.get(key);
        const parts = ids.map(id => new Uint8Array(images[id]));
        const bytes = new Uint8Array(parts.reduce((size, part) => size + 4 + part.length, 0));
        const lengths = new DataView(bytes.buffer);
        let offset = 0;
        for (const part of parts) {
            lengths.setUint32(offset, part.length, true);
            bytes.set(part, offset + 4);
            offset += part.length + 4;
        }
        const linked = upload(bytes);
        bundles.set(key, linked);
        return linked;
    }

    const results = [];
    for (const test of cases) {
        const entries = new Uint8Array(11);
        new DataView(entries.buffer).setUint32(0, test.row, true);
        entries.set([3, 0, 0, 0, 114, 117, 110], 4);
        if (test.status !== undefined)
            for (let i = 0; i < capacity; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle(test.images), upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(index => build.ReadDiagnostic(location, index));

        if (test.status !== undefined) {
            if (status !== test.status || diagnostic.join(",") !== [0, test.row, test.offset].join(","))
                throw Error(test.name + " returned " + status + " at " + diagnostic);
            for (let i = 0; i < capacity; i++)
                if (build.ReadByte(output, i) !== 0xa5) throw Error(test.name + " changed the output on failure");
            results.push({ status, diagnostic });
            continue;
        }

        if (status < 1 || status > capacity) throw Error(test.name + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const { instance, module } = await WebAssembly.instantiate(code);
        if (WebAssembly.Module.imports(module).length) throw Error(test.name + " has runtime imports");
        const values = test.inputs.map(input => instance.exports.run(Number(input)));
        if (values.some((value, index) => value !== test.expected[index]))
            throw Error(test.name + " disagrees with the CLR");
        results.push({ values, bytes: Array.from(code) });
    }
    return results;
}

const node = await runCases(files[0], files.slice(1), oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ count, cases, source }) => {
        const [compiler, ...images] = await Promise.all(Array.from({ length: count }, async (_, i) =>
            (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, images, cases);
    }, { count: files.length, cases: oracle.cases, source: runCases.toString() });
    assert.deepEqual(edge, node);
    const positive = oracle.cases.reduce((count, test) => count + (test.expected?.length ?? 0), 0);
    const diagnostics = oracle.cases.filter(test => test.status !== undefined).length;
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, pack: oracle.pack, node: process.version, edge: browser.version(), positive, diagnostics
    }, null, 2) + "\n");
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + positive +
        " CLR values and " + diagnostics + " facade diagnostics on .NET " + oracle.runtime + ".");
} finally { await browser.close(); }
