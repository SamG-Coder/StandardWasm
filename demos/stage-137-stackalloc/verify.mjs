import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 137 (decision 0065): stackalloc into primitive pointers on the shadow stack in
// memory 0. The x86 CLR is the oracle for every stateless kernel. A Wasm-only sequence
// exhausts the shadow stack inside a nested frame, and the next host call must still
// find an empty stack on the same instance.
const stage = join(root, "demos", "stage-137-stackalloc");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "stackalloc");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "StackAllocFixture.dll");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [readFileSync(compiler), readFileSync(fixture)];

async function runCases(compiler, fixture, oracle) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 20;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();

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

    async function compile(exports) {
        const entries = new Uint8Array(exports.reduce((size, [, name]) => size + 8 + name.length, 0));
        let cursor = 0;
        for (const [row, name] of exports) {
            const view = new DataView(entries.buffer);
            view.setUint32(cursor, row, true);
            view.setUint32(cursor + 4, name.length, true);
            for (let i = 0; i < name.length; i++) entries[cursor + 8 + i] = name.charCodeAt(i);
            cursor += 8 + name.length;
        }
        const status = build.CompileBundleDetailed(bundle, upload(entries), output, location);
        if (status < 1 || status > capacity)
            throw Error("rows " + exports.map(e => e[0]) + " returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
        const module = await WebAssembly.compile(Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i)));
        const imports = WebAssembly.Module.imports(module).map(entry => entry.module + "." + entry.name + ":" + entry.kind);
        if (imports.join() !== "standardwasm:resources.memory:memory") throw Error("unexpected imports " + imports.join());
        return module;
    }

    const lend = () => {
        const memory = new WebAssembly.Memory({ initial: 1 });
        return { memory, imports: { "standardwasm:resources": { memory } } };
    };

    const results = [];
    for (const test of oracle.cases) {
        const { imports } = lend();
        const instance = await WebAssembly.instantiate(await compile([[test.row, "run"]]), imports);
        const value = String(instance.exports.run(...test.arguments));
        if (value !== test.expected) throw Error(test.name + "(" + test.arguments + ") returned " + value + ", the CLR returned " + test.expected);
        results.push({ name: test.name, arguments: test.arguments, value });
    }

    // Same instance: run traps (engine trap, never a managed exception), after works.
    const { memory, imports } = lend();
    const pair = await WebAssembly.instantiate(await compile([[oracle.exhaust.row, "run"], [oracle.exhaust.after, "after"]]), imports);
    const sequence = [];
    for (const name of ["run", "after", "run", "after", "after"]) {
        try { sequence.push(name + "=" + pair.exports[name]()); }
        catch (error) {
            if (error instanceof WebAssembly.Exception || !(error instanceof WebAssembly.RuntimeError))
                throw Error(name + " raised " + error + " instead of an engine trap");
            sequence.push(name + "=trap");
        }
    }
    const expected = ["run=trap", "after=" + oracle.exhaust.afterValue, "run=trap",
        "after=" + oracle.exhaust.afterValue, "after=" + oracle.exhaust.afterValue];
    if (sequence.join() !== expected.join()) throw Error("exhaustion sequence " + sequence + ", expected " + expected);
    // The shadow stack is reserved once per instance: 1 initial page + 16.
    results.push({ name: "ExhaustionRecovery", sequence, pages: memory.buffer.byteLength / 65536 });

    // A host memory that cannot grow: a zero-sized stackalloc never reserves the region,
    // so it succeeds (null); the first real allocation is an engine trap (failed grow).
    const fixed = new WebAssembly.Memory({ initial: 1, maximum: 1 });
    const zero = await WebAssembly.instantiate(await compile([[oracle.zero, "run"]]), { "standardwasm:resources": { memory: fixed } });
    const zeroResult = String(zero.exports.run(0));
    if (zeroResult !== "1") throw Error("zero-sized stackalloc on a fixed memory returned " + zeroResult);
    let grew;
    try { grew = "returned " + zero.exports.run(1); }
    catch (error) {
        if (error instanceof WebAssembly.Exception || !(error instanceof WebAssembly.RuntimeError))
            throw Error("fixed memory raised " + error + " instead of an engine trap");
        grew = "trap";
    }
    if (grew !== "trap") throw Error("allocation on a fixed memory " + grew);
    results.push({ name: "FixedMemory", zero: zeroResult, nonzero: grew, pages: fixed.buffer.byteLength / 65536 });

    // An untaken allocation never grows a one-page memory.
    const untaken = await WebAssembly.instantiate(await compile([[oracle.untaken, "run"]]),
        { "standardwasm:resources": { memory: new WebAssembly.Memory({ initial: 1, maximum: 1 }) } });
    if (String(untaken.exports.run(0)) !== "79") throw Error("untaken stackalloc did not return 79");

    // Room for exactly one reservation: a second memory.grow after the callee reserved
    // the region would trap. Three calls on one instance must all match the CLR.
    const bounded = new WebAssembly.Memory({ initial: 1, maximum: 1 + 16 });
    const outer = await WebAssembly.instantiate(await compile([[oracle.outer, "run"]]), { "standardwasm:resources": { memory: bounded } });
    const outerValues = [4, 4, 4].map(n => String(outer.exports.run(n)));
    if (outerValues.some(v => v !== oracle.outerValue)) throw Error("outer-after-inner returned " + outerValues + ", CLR " + oracle.outerValue);
    results.push({ name: "Reservation", untaken: "79", outer: outerValues, pages: bounded.buffer.byteLength / 65536 });
    return results;
}

const node = await runCases(files[0], files[1], oracle);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ oracle, source }) => {
        const [compiler, fixture] = await Promise.all([0, 1].map(async i => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, fixture, oracle);
    }, { oracle, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + oracle.cases.length +
        " stackalloc results from the x86 CLR " + oracle.runtime +
        "; exhaustion is an engine trap and the next host call finds an empty shadow stack.");
} finally { await browser.close(); }
