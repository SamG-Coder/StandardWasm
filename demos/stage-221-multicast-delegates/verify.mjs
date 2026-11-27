import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 221 (issue #256, decision 0092): multicast general delegates. Func/Action instantiations
// and delegate TypeDefs share EventHandler's invocation list with a nominal delegate type, so
// Delegate.Combine/Remove, field-like events, CompareExchange and checked casts from Delegate or
// object work. The x86 CLR runs the same DLL as the oracle; Node and Edge must return its values
// and must diagnose (status -2) the Reject* kernels.
const stage = join(root, "demos", "stage-221-multicast-delegates");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "multicast-delegates");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "MulticastDelegateFixture.dll");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 22;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();
    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }
    const parts = images.map(image => new Uint8Array(image));
    const packed = new Uint8Array(parts.reduce((size, part) => size + 4 + part.length, 0));
    let offset = 0;
    for (const part of parts) {
        new DataView(packed.buffer).setUint32(offset, part.length, true);
        packed.set(part, offset + 4);
        offset += 4 + part.length;
    }
    const bundle = upload(packed);
    const results = [];
    for (const test of cases) {
        const bytes = new Uint8Array(11);
        new DataView(bytes.buffer).setUint32(0, test.row, true);
        bytes[4] = 3;
        bytes.set([0x72, 0x75, 0x6e], 8);
        const status = build.CompileBundleDetailed(bundle, upload(bytes), output, location);
        if (test.rejected) {
            if (status !== -2) throw Error(test.name + " must be diagnosed (-2) but returned " + status);
            results.push({ name: test.name, value: "diagnosed" });
            continue;
        }
        if (status < 1 || status > capacity)
            throw Error(test.name + " returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const instance = await WebAssembly.instantiate(await WebAssembly.compile(code));
        const value = String(instance.exports.run());
        if (value !== test.expected) throw Error(test.name + " returned " + value + ", the CLR " + test.expected);
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
    const edge = await page.evaluate(async ({ cases, count, source }) => {
        const bytes = await Promise.all(Array.from({ length: count }, async (_, i) => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(bytes[0], bytes.slice(1), cases);
    }, { cases: oracle.cases, count: files.length, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + node.length +
        " multicast delegate results from the x86 CLR " + oracle.runtime +
        " (order, removal, events, Delegate slots and nominal casts; Func<int> and equality diagnosed).");
} finally { await browser.close(); }
