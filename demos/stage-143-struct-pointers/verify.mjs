import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 143 (decision 0071): sizeof and pointers to blittable sequential structs in the host-lent
// memory 0. The x86 CLR runs each kernel over Marshal.AllocHGlobal memory; the runner
// places the same bytes in a fresh WebAssembly.Memory at a base address and passes the
// same offsets. Both the return value and the memory image afterwards must match.
const stage = join(root, "demos", "stage-143-struct-pointers");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "struct-pointers");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "StructPointerFixture.dll");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const decode = text => Array.from(Buffer.from(text, "base64"));
const cases = oracle.cases.map(test => ({ ...test, image: decode(test.image), after: decode(test.after) }));
const files = [readFileSync(compiler), readFileSync(fixture)];

async function runCases(compiler, fixture, cases, imageSize) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 20;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();
    const base = 1024; // never address 0: null stays distinct from data

    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }

    const image = new Uint8Array(fixture);
    const packed = new Uint8Array(image.length + 4);
    new DataView(packed.buffer).setUint32(0, image.length, true);
    packed.set(image, 4);
    const bundle = upload(packed);
    const compiled = new Map();

    // A located diagnostic: the compiler returns status at [module, row, IL offset] and
    // leaves the output buffer untouched.
    function diagnose(test) {
        const entries = new Uint8Array(11);
        new DataView(entries.buffer).setUint32(0, test.row, true);
        entries[4] = 3;
        entries.set([0x72, 0x75, 0x6e], 8);
        for (let i = 0; i < 4096; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle, upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
        if (status !== test.status || diagnostic.join() !== test.diagnostic.join())
            throw Error(test.name + " returned " + status + " at " + diagnostic + ", expected " + test.status + " at " + test.diagnostic);
        for (let i = 0; i < 4096; i++) if (build.ReadByte(output, i) !== 0xa5) throw Error(test.name + " changed the output on failure");
        return { name: test.name, status, diagnostic, clr: test.expected };
    }

    async function module(row) {
        if (compiled.has(row)) return compiled.get(row);
        const entries = new Uint8Array(11);
        new DataView(entries.buffer).setUint32(0, row, true);
        entries[4] = 3;
        entries.set([0x72, 0x75, 0x6e], 8);
        const status = build.CompileBundleDetailed(bundle, upload(entries), output, location);
        if (status < 1 || status > capacity)
            throw Error("row " + row + " returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const result = await WebAssembly.compile(code);
        const imports = WebAssembly.Module.imports(result).map(entry => entry.module + "." + entry.name + ":" + entry.kind);
        if (imports.length && imports.join() !== "standardwasm:resources.memory:memory")
            throw Error("row " + row + " imports " + imports.join());
        compiled.set(row, result);
        return result;
    }

    const results = [];
    for (const test of cases) {
        if (test.status !== undefined) { results.push(diagnose(test)); continue; }
        const memory = new WebAssembly.Memory({ initial: 1 });
        const initialBytes = new Uint8Array(memory.buffer);
        const borrowedLength = initialBytes.length;
        initialBytes.set(test.image, base);
        // sizeof-only kernels need no memory, so they import nothing.
        const compiledModule = await module(test.row);
        const instance = await WebAssembly.instantiate(compiledModule,
            WebAssembly.Module.imports(compiledModule).length ? { "standardwasm:resources": { memory } } : undefined);
        const args = test.arguments.map(argument =>
            argument.kind === "pointer" ? base + argument.value : argument.value);
        let value;
        try { value = instance.exports.run(...args); }
        catch (error) {
            if (!test.wasmOnly) throw Error(test.name + " threw " + error);
            // An engine trap, not a managed exception on a Wasm tag.
            if (error instanceof WebAssembly.Exception || !(error instanceof WebAssembly.RuntimeError))
                throw Error(test.name + " raised " + error + " instead of an engine trap");
            results.push({ name: test.name, outcome: "engine trap" });
            continue;
        }
        if (test.wasmOnly) throw Error(test.name + " returned " + value + " instead of trapping");
        const text = typeof value === "bigint" ? value.toString() : test.result === "Double" ? String(value) : String(value);
        // A native local can grow memory for the shadow stack. Refresh the
        // view after the call and compare the caller's original page.
        const bytes = new Uint8Array(memory.buffer, 0, borrowedLength);
        const after = Array.from(bytes.slice(base, base + imageSize));
        if (text !== test.expected) throw Error(test.name + " returned " + text + ", the CLR returned " + test.expected);
        if (after.some((byte, i) => byte !== test.after[i])) throw Error(test.name + " left memory different from the CLR");
        // Nothing outside the image changed: a stray store would show up here.
        if (bytes.slice(0, base).some(byte => byte !== 0) || bytes.slice(base + imageSize).some(byte => byte !== 0))
            throw Error(test.name + " wrote outside its image");
        results.push({ name: test.name, value: text, memory: "same" });
    }
    return results;
}

const node = await runCases(files[0], files[1], cases, oracle.imageSize);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ cases, imageSize, source }) => {
        const [compiler, fixture] = await Promise.all([0, 1].map(async i => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, fixture, cases, imageSize);
    }, { cases, imageSize: oracle.imageSize, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " sizeof and struct-pointer results and memory images from the x86 CLR " + oracle.runtime + "; Pack and Size stay " +
        node.filter(result => result.status !== undefined).length + " located diagnostics.");
} finally { await browser.close(); }
