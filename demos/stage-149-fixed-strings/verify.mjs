import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

// Stage 149 (decision 0077): fixed (char* p = s) reads a frame-owned UTF-16 copy of s with
// a NUL terminator on the Stage 137 shadow stack. The same kernels are built twice: for
// netstandard2.0 (pinned string + RuntimeHelpers.OffsetToStringData) and for net10.0
// (pinned char& from string.GetPinnableReference()). The CLR runs both builds; the
// self-hosted compiler compiles the same rows and Node and Edge must return the same values.
const stage = join(root, "demos", "stage-149-fixed-strings");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "fixed-strings");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
run("dotnet", ["build", "-c", "Release", join(stage, "FixtureNet")]);
const fixtures = [
    join(stage, "Fixture", "bin", "Release", "netstandard2.0", "FixedStringFixture.dll"),
    join(stage, "FixtureNet", "bin", "Release", "net10.0", "FixedStringNetFixture.dll")
];
run("dotnet", ["run", "-c", "Release", "--project", join(stage, "Oracle"), "--", evidence, ...fixtures]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
const files = [readFileSync(compiler), ...fixtures.map(path => readFileSync(path))];
const modules = oracle.modules.map(({ cases, rejects }) => ({ cases, rejects }));

async function runModules(compiler, fixtures, modules) {
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

    const results = [];
    for (let m = 0; m < modules.length; m++) {
        const image = new Uint8Array(fixtures[m]);
        const packed = new Uint8Array(image.length + 4);
        new DataView(packed.buffer).setUint32(0, image.length, true);
        packed.set(image, 4);
        const bundle = upload(packed);
        const compiled = new Map();
        for (const test of modules[m].cases) {
            if (!compiled.has(test.row)) {
                const status = build.CompileBundleDetailed(bundle, entries(test.row), output, location);
                if (status < 1 || status > capacity)
                    throw Error(test.name + " returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(location, i)));
                const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
                const module = await WebAssembly.compile(code);
                const imports = WebAssembly.Module.imports(module).map(entry => entry.module + "." + entry.name + ":" + entry.kind);
                // Pinned strings live in the host-lent memory 0, and nothing else is imported.
                if (imports.join() !== "standardwasm:resources.memory:memory")
                    throw Error(test.name + " imports " + imports.join());
                compiled.set(test.row, module);
            }
            const memory = new WebAssembly.Memory({ initial: 1 });
            const instance = await WebAssembly.instantiate(compiled.get(test.row), { "standardwasm:resources": { memory } });
            const value = String(instance.exports.run(...test.inputs));
            if (value !== test.expected)
                throw Error(test.name + "(" + test.inputs + ") returned " + value + ", the CLR returned " + test.expected);
            results.push({ module: m, name: test.name, inputs: test.inputs, value });
        }
        // A located diagnostic: the status at [module, row, IL offset].
        for (const test of modules[m].rejects) {
            const status = build.CompileBundleDetailed(bundle, entries(test.row), output, location);
            const diagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
            if (status !== test.status || diagnostic.join() !== [0, test.row, test.offset].join())
                throw Error(test.name + " returned " + status + " at " + diagnostic);
            results.push({ module: m, name: test.name, status, diagnostic });
        }
    }
    return results;
}

const node = await runModules(files[0], files.slice(1), modules);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ modules, count, source }) => {
        const bytes = await Promise.all(Array.from({ length: count }, async (_, i) => (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(bytes[0], bytes.slice(1), modules);
    }, { modules, count: files.length, source: runModules.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " fixed-string results in both IL forms from the CLR " + oracle.runtime + " plus " +
        (node.length - values) + " located diagnostics.");
} finally { await browser.close(); }
