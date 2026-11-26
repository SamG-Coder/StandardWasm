import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 126 (decision 0054): throw and catch managed exception objects of linked classes.
// The fixture derives from the authentic System.Exception, so each bundle carries the
// supported 32-bit System.Runtime and CoreLib, and the x86 CLR running the same DLL is the oracle.
const stage = join(root, "demos", "stage-126-managed-exception-objects");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "managed-exception-objects");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const bin = join(stage, "Fixture", "bin", "Release", "net10.0");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, join(bin, "ExceptionFixture.dll"), join(bin, "ExceptionTwin.dll")]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 20;
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
            for (let i = 0; i < 4096; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle(test.images), upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(index => build.ReadDiagnostic(location, index));

        if (test.status !== undefined) {
            if (status !== test.status || diagnostic.join(",") !== test.diagnostic.join(","))
                throw Error(test.name + " returned " + status + " at " + diagnostic + ", expected " +
                    test.status + " at " + test.diagnostic);
            for (let i = 0; i < 4096; i++)
                if (build.ReadByte(output, i) !== 0xa5) throw Error(test.name + " changed the output on failure");
            results.push({ name: test.name, status, diagnostic });
            continue;
        }

        if (status < 1 || status > capacity) throw Error(test.name + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const { instance, module } = await WebAssembly.instantiate(code);
        if (WebAssembly.Module.imports(module).length) throw Error(test.name + " has runtime imports");
        if (test.uncaught) {
            // The uncaught managed object must reach the host on the exported object tag,
            // never as a Wasm trap or a fixed-tag CLR exception.
            const tag = instance.exports["\u0000object"];
            if (!(tag instanceof WebAssembly.Tag)) throw Error(test.name + " does not export the object tag");
            let caught;
            try { instance.exports.run(); } catch (error) { caught = error; }
            if (!(caught instanceof WebAssembly.Exception) || !caught.is(tag))
                throw Error(test.name + " did not raise the object tag: " + caught);
            results.push({ name: test.name, uncaught: "object tag" });
            continue;
        }
        const value = String(instance.exports.run());
        if (value !== test.wasm) throw Error(test.name + " returned " + value + ", the CLR returned " + test.clr);
        results.push({ name: test.name, value });
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
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " managed exception results from the x86 CLR " + oracle.runtime +
        ", the filter diagnostic and the uncaught object tag.");
} finally { await browser.close(); }
