import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 153 (issue #299, decision 0080): an assembly missing from the bundle. The CLR runs the
// fixture with MissingDependency.dll absent: untouched code runs, and each call into a method
// that needs the dependency throws FileNotFoundException. The self-hosted compiler's opt-in
// CompileBundleDeferred stubs exactly those methods with the bundled CoreLib's own
// FileNotFoundException, and Node and Edge must return the CLR's values. The strict
// CompileBundleDetailed keeps its located -4.
const stage = join(root, "demos", "stage-153-missing-dependencies");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "missing-dependencies");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "net10.0", "MissingDependencyFixture.dll");
run("dotnet", ["run", "-c", "Release", "--project", join(stage, "Oracle"), "--", evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
// The 32-bit CoreLib and System.Runtime, as other CoreLib-backed stages bundle them. The
// dependency is deliberately not in the bundle.
const runtime = wasm32Runtime();
const images = [fixture, join(runtime.directory, "System.Runtime.dll"), join(runtime.directory, "System.Private.CoreLib.dll")];
const files = [compiler, ...images].map(path => readFileSync(path));
const cases = oracle.cases.map(({ name, row, inputs, expected, exception }) => ({ name, row, inputs, expected, exception }));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 22;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();
    const warnings = build.CreateWarnings(16);

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

    function entries(row) {
        const bytes = new Uint8Array(11);
        new DataView(bytes.buffer).setUint32(0, row, true);
        bytes[4] = 3;
        bytes.set([0x72, 0x75, 0x6e], 8);
        return upload(bytes);
    }

    const results = [];
    const compiled = new Map();
    for (const test of cases) {
        if (!compiled.has(test.row)) {
            const strict = build.CompileBundleDetailed(bundle, entries(test.row), output, location);
            const strictDiagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
            const status = build.CompileBundleDeferred(bundle, entries(test.row), output, location, warnings);
            if (status < 1 || status > capacity)
                throw Error(test.name + " deferred returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
            const count = build.ReadWarning(warnings, 0);
            const stubs = Array.from({ length: count }, (_, i) => [1, 2, 3].map(j => build.ReadWarning(warnings, 3 * i + j)));
            const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
            const module = await WebAssembly.compile(code);
            if (WebAssembly.Module.imports(module).length) throw Error(test.name + " should import nothing");
            compiled.set(test.row, { module, strict, strictDiagnostic, stubs });
        }
        const { module, strict, strictDiagnostic, stubs } = compiled.get(test.row);
        const instance = await WebAssembly.instantiate(module);
        let value;
        try { value = String(instance.exports.run(...test.inputs)); }
        catch (error) {
            // A managed exception reaches the host on the object tag; a trap is not one.
            if (!(error instanceof WebAssembly.Exception)) throw Error(test.name + " trapped: " + error);
            value = "managed exception";
        }
        const want = test.expected ?? "managed exception";
        if (value !== want) throw Error(test.name + "(" + test.inputs + ") returned " + value + ", the CLR " + (test.exception ?? want));
        results.push({ name: test.name, inputs: test.inputs, value, strict, strictDiagnostic, stubs });
    }
    return results;
}

const node = await runCases(files[0], files.slice(1), cases);
// Methods that need nothing from the dependency compile in strict mode too; the others keep
// the located -4 there, and deferred mode names each stubbed method.
for (const result of node) {
    const clean = result.stubs.length === 0;
    if (clean && result.name !== "ReferenceFieldOnly") assert.ok(result.strict > 0, result.name + " strict " + result.strict);
    else {
        assert.equal(result.strict, -4, result.name + " strict status");
        assert.equal(result.strictDiagnostic[0], 0);
    }
}
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
    }, { cases, count: files.length, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(),
        clrCases: oracle.cases, results: node
    }, null, 2) + "\n");
    const stubbed = new Set(node.flatMap(result => result.stubs.map(stub => stub.join(":"))));
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + node.length +
        " CLR results with the dependency absent, " + stubbed.size + " stubbed methods throwing CoreLib's FileNotFoundException.");
} finally { await browser.close(); }
