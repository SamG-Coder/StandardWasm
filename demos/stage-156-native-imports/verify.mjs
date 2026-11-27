import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import * as host from "../../scripts/native-imports.mjs";

// Stage 156 (issue #274, decision 0083): blittable scalar P/Invokes and InternalCalls compile to
// typed Wasm imports. The x86 CLR runs the fixture against the real kernel32 and ucrtbase as the
// oracle; Node and Edge run the same DLL with scripts/native-imports.mjs as the host registry and
// must return the CLR's values. An entry the registry lacks raises CoreLib's
// EntryPointNotFoundException; a non-blittable signature is a located compile diagnostic.
const stage = join(root, "demos", "stage-156-native-imports");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "native-imports");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "NativeImportFixture.dll");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases, host) {
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
        const diagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
        if (test.status !== undefined) {
            // A planned diagnostic: the exact status at the exact method and IL offset.
            if (status !== test.status || diagnostic.join() !== test.diagnostic.join())
                throw Error(test.name + " returned " + status + " at " + diagnostic + ", expected " + test.status + " at " + test.diagnostic);
            results.push({ name: test.name, status, diagnostic });
            continue;
        }
        if (status < 1 || status > capacity) throw Error(test.name + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const module = await WebAssembly.compile(code);
        // A fresh host state per instance: the last-error slots belong to one CLR thread.
        const instance = await WebAssembly.instantiate(module, host.nativeImports(module, host.windowsScalars));
        const value = String(instance.exports.run());
        if (value !== test.expected) throw Error(test.name + " returned " + value + ", the CLR " + test.expected);
        results.push({ name: test.name, value, imports: host.nativeEntries(module).map(entry => entry.key) });
    }
    return results;
}

const node = await runCases(files[0], files.slice(1), oracle.cases, host);
// The registry names an entry the CLR resolved as missing; it must not be implemented by the host.
assert.ok(node.some(result => result.imports?.includes("kernel32!StandardWasmNoSuchEntry")));
assert.ok(!Object.hasOwn(host.windowsScalars, "kernel32!StandardWasmNoSuchEntry"));
// The absent library must be unknown to the registry, while kernel32 is known: the two failures
// raise different CoreLib exceptions (DllNotFoundException, EntryPointNotFoundException).
assert.ok(node.some(result => result.imports?.includes("standardwasmabsentlibrary!Anything")));
assert.ok(!Object.keys(host.windowsScalars).some(key => key.startsWith("standardwasmabsentlibrary!")));
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const registry = readFileSync(join(root, "scripts", "native-imports.mjs"));
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        if (path === "/native-imports.mjs") return route.fulfill({ contentType: "text/javascript", body: registry });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ cases, count, source }) => {
        const host = await import("/native-imports.mjs");
        const bytes = await Promise.all(Array.from({ length: count }, async (_, i) => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(bytes[0], bytes.slice(1), cases, host);
    }, { cases: oracle.cases, count: files.length, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " native-import results from the x86 CLR " + oracle.runtime + " (kernel32, ucrtbase, SetLastError, a missing" +
        " entry point and a missing library) and " + (node.length - values) + " located non-blittable diagnostic.");
} finally { await browser.close(); }
