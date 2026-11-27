import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import * as host from "../../scripts/native-imports.mjs";

// Stage 365 (issue #275, decision 0083 step 2): unmanaged pointer parameters and results in typed
// native imports. The x86 CLR runs the fixture against the real kernel32 and ucrtbase. Node and
// Edge run the same DLL with scripts/native-imports.mjs as the registry, over the memory the
// host lends the module, and must return the CLR's values. Byrefs, strings, arrays, delegates,
// bool/char, by-value structs and pointers to bool, char or a non-reproducible struct layout
// remain located -2 diagnostics at the import.
const stage = join(root, "demos", "stage-365-native-pointer-arguments");
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compilerPath)) throw Error("Build the self-hosted compiler first (see the README).");
const output = join(root, "artifacts", "native-pointer-arguments");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const fixture = join(stage, "Fixture", "bin", "Release", "netstandard2.0", "NativePointerFixture.dll");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, fixture]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [compilerPath, ...oracle.files].map(path => readFileSync(path));
const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");

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
    const registry = { ...host.windowsScalars, ...host.windowsPointers };
    const results = [];
    for (const test of cases) {
        const bytes = new Uint8Array(11);
        new DataView(bytes.buffer).setUint32(0, test.row, true);
        bytes[4] = 3;
        bytes.set([0x72, 0x75, 0x6e], 8);
        const status = build.CompileBundleDetailed(bundle, upload(bytes), output, location);
        const diagnostic = [0, 1, 2].map(i => build.ReadDiagnostic(location, i));
        if (test.status !== undefined) {
            // A planned diagnostic: the exact status at the import's MethodDef.
            if (status !== test.status || diagnostic.join() !== test.diagnostic.join())
                throw Error(test.name + " returned " + status + " at " + diagnostic + ", expected " + test.status + " at " + test.diagnostic);
            results.push({ name: test.name, status, diagnostic });
            continue;
        }
        if (status < 1 || status > capacity) throw Error(test.name + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const module = await WebAssembly.compile(code);
        // A fresh memory and last-error state per instance. The registry dereferences pointers
        // through state.memory, the same memory the module imports.
        const memory = new WebAssembly.Memory({ initial: 1 });
        const state = { system: 0, pinvoke: 0, memory };
        const imports = host.nativeImports(module, registry, state);
        const lent = WebAssembly.Module.imports(module).some(entry =>
            entry.module === "standardwasm:resources" && entry.name === "memory" && entry.kind === "memory");
        if (lent) imports["standardwasm:resources"] = { memory };
        const instance = await WebAssembly.instantiate(module, imports);
        const value = String(instance.exports.run());
        if (value !== test.expected) throw Error(test.name + " returned " + value + ", the CLR " + test.expected);
        results.push({ name: test.name, value, memory: lent, imports: host.nativeEntries(module).map(entry => entry.key) });
    }
    return results;
}

const node = await runCases(files[0], files.slice(1), oracle.cases, host);
// Every pointer entry was reached through a typed import, and every kernel that builds an
// address in memory 0 imported that memory.
for (const key of Object.keys(host.windowsPointers))
    assert.ok(node.some(result => result.imports?.includes(key)), key + " was not imported");
assert.ok(node.filter(result => result.value !== undefined && result.name !== "NullPointers").every(result => result.memory));
const diagnostics = node.filter(result => result.status !== undefined).map(result => result.name);
assert.deepEqual(diagnostics, ["ByReference", "ReadOnlyReference", "ByteArray", "AnsiString", "DelegateArgument",
    "BooleanArgument", "CharacterArgument", "StructByValue", "BooleanPointer", "CharacterPointer", "PackedPointer",
    "AutoLayoutPointer"]);
// Dereferencing entries refuse null and addresses past the memory instead of reading host data.
{
    const memory = new WebAssembly.Memory({ initial: 1 });
    const state = { system: 0, pinvoke: 0, memory };
    assert.throws(() => host.windowsPointers["ucrtbase!strlen"](state, 0), /access violation/);
    assert.throws(() => host.windowsPointers["kernel32!FileTimeToSystemTime"](state, 65536 - 4, 16), /access violation/);
    assert.throws(() => host.windowsPointers["ucrtbase!memcpy"]({ system: 0, pinvoke: 0 }, 16, 32, 1), /state.memory/);
}
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
    const values = node.filter(result => result.value !== undefined).length;
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: { path: compilerPath, sha256: sha256(files[0]) }, fixture: sha256(files[1]),
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    console.log("Compiler SHA-256 " + sha256(files[0]) + ".");
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " pointer native-import results from the x86 CLR " + oracle.runtime + " (FileTimeToSystemTime, SystemTimeToFileTime" +
        " with SetLastError failures, strlen and memcpy over fixed, fixed-buffer and stackalloc memory, a pointer-to-pointer" +
        " copy and null pointers) and " + (node.length - values) + " located -2 diagnostics.");
} finally { await browser.close(); }
