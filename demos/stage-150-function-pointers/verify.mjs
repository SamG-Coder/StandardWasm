import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

// Stage 150 (decision 0078): managed function pointers. ldftn of a static method is a slot in
// table 2 and calli is call_indirect through it. The CLR runs each kernel; the self-hosted
// compiler compiles the same rows and Node and Edge must return the same values. A null
// call is an engine trap in Wasm (the CLR's access violation ends the process).
const stage = join(root, "demos", "stage-150-function-pointers");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "function-pointers");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "FunctionPointerFixture.dll");
run("dotnet", ["run", "-c", "Release", "--project", join(stage, "Oracle"), "--", evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
// The dependency holds an ordinary (object, nint) constructor and a delegate type reached by MemberRef.
const dependency = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "FunctionPointerDependency.dll");
const files = [readFileSync(compiler), readFileSync(fixture), readFileSync(dependency)];
const tests = { cases: oracle.cases, traps: oracle.traps, rejects: oracle.rejects };

async function runCases(compiler, fixture, dependency, tests) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 20;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();

    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }

    function entries(row) {
        const bytes = new Uint8Array(11);
        new DataView(bytes.buffer).setUint32(0, row, true);
        bytes[4] = 3;
        bytes.set([0x72, 0x75, 0x6e], 8);
        return upload(bytes);
    }

    // Bundle: [u32 length][assembly] for the fixture, then its dependency.
    const images = [new Uint8Array(fixture), new Uint8Array(dependency)];
    const packed = new Uint8Array(images.reduce((total, image) => total + 4 + image.length, 0));
    let cursor = 0;
    for (const image of images) {
        new DataView(packed.buffer).setUint32(cursor, image.length, true);
        packed.set(image, cursor + 4);
        cursor += 4 + image.length;
    }
    const bundle = upload(packed);
    const compiled = new Map();

    async function instantiate(test) {
        if (!compiled.has(test.row)) {
            const status = build.CompileBundleDetailed(bundle, entries(test.row), output, location);
            if (status < 1 || status > capacity)
                throw Error(test.name + " returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
            const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
            compiled.set(test.row, await WebAssembly.compile(code));
        }
        const module = compiled.get(test.row);
        const imports = WebAssembly.Module.imports(module).map(entry => entry.module + "." + entry.name + ":" + entry.kind);
        // Only kernels that use stackalloc need memory 0; nothing else is imported.
        if (imports.length && imports.join() !== "standardwasm:resources.memory:memory")
            throw Error(test.name + " imports " + imports.join());
        const memory = new WebAssembly.Memory({ initial: 1 });
        return (await WebAssembly.instantiate(module, imports.length ? { "standardwasm:resources": { memory } } : undefined)).exports.run;
    }

    const results = [];
    for (const test of tests.cases) {
        const value = String((await instantiate(test))(...test.inputs));
        if (value !== test.expected)
            throw Error(test.name + "(" + test.inputs + ") returned " + value + ", the CLR returned " + test.expected);
        results.push({ name: test.name, inputs: test.inputs, value });
    }
    for (const test of tests.traps) {
        const run = await instantiate(test);
        try { run(...test.inputs); }
        catch (error) {
            // An engine trap, not a managed exception on a Wasm tag.
            if (error instanceof WebAssembly.Exception || !(error instanceof WebAssembly.RuntimeError))
                throw Error(test.name + " raised " + error + " instead of an engine trap");
            results.push({ name: test.name, outcome: "engine trap" });
            continue;
        }
        throw Error(test.name + " returned instead of trapping");
    }
    // A located diagnostic: the status at [module, row, IL offset].
    for (const test of tests.rejects) {
        const status = build.CompileBundleDetailed(bundle, entries(test.row), output, location);
        const diagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
        if (status !== test.status || diagnostic.join() !== [0, test.row, test.offset].join())
            throw Error(test.name + " returned " + status + " at " + diagnostic);
        results.push({ name: test.name, status, diagnostic });
    }
    return results;
}

const node = await runCases(files[0], files[1], files[2], tests);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ tests, source }) => {
        const [compiler, fixture, dependency] = await Promise.all([0, 1, 2].map(async i => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, fixture, dependency, tests);
    }, { tests, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " function-pointer results from the CLR " + oracle.runtime + "; a null call is an engine trap and " +
        tests.rejects.length + " located diagnostic remains (unmanaged calli).");
} finally { await browser.close(); }
