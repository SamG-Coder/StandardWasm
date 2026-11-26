import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-113-corelib-native-width");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "corelib-native-width");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

// The 32-bit CLR is both the oracle and the supported pack. The 64-bit pack is
// the same release, which isolates the native width from the version.
const x86Host = process.env.STANDARDWASM_X86_DOTNET || "C:/Program Files (x86)/dotnet/dotnet.exe";
if (!existsSync(x86Host)) throw Error("Install the win-x86 .NET 10 runtime or set STANDARDWASM_X86_DOTNET.");
function coreRuntimes(host) {
    const listed = execFileSync(host, ["--list-runtimes"], { encoding: "utf8" });
    return new Map([...listed.matchAll(/^Microsoft\.NETCore\.App (10\.\S+) \[(.+)\]$/gm)]
        .map(([, version, directory]) => [version, join(directory, version)]));
}
const narrowRuntimes = coreRuntimes(x86Host);
const wideRuntimes = coreRuntimes("dotnet");
const version = [...narrowRuntimes.keys()].filter(v => wideRuntimes.has(v)).sort().at(-1);
if (!version) throw Error("Install matching win-x86 and win-x64 .NET 10 runtimes.");
const widePack = process.env.STANDARDWASM_X64_RUNTIME_PACK || wideRuntimes.get(version);
const browserPack = process.env.STANDARDWASM_BROWSER_WASM_PACK ||
    join(homedir(), ".nuget", "packages", "microsoft.netcore.app.runtime.mono.browser-wasm", version);

run("dotnet", ["build", "-c", "Release", join(stage, "Consumer")]);
run("dotnet", ["build", "-c", "Release", join(stage, "Oracle")]);
const consumer = join(stage, "Consumer", "bin", "Release", "net10.0", "NativeWidthConsumer.dll");
const oracleDll = join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll");
run(x86Host, ["exec", "--fx-version", version, oracleDll, evidence, consumer, widePack,
    ...(existsSync(browserPack) ? [browserPack] : [])]);
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
        const label = test.name + " on " + test.pack;
        if (test.status !== undefined)
            for (let i = 0; i < 4096; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle(test.images), upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(index => build.ReadDiagnostic(location, index));

        if (test.status !== undefined) {
            if (status !== test.status || diagnostic.join(",") !== test.diagnostic.join(","))
                throw Error(label + " returned " + status + " at " + diagnostic);
            for (let i = 0; i < 4096; i++)
                if (build.ReadByte(output, i) !== 0xa5) throw Error(label + " changed the output on failure");
            results.push({ name: test.name, pack: test.pack, status, diagnostic });
            continue;
        }

        if (status < 1 || status > capacity) throw Error(label + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const { instance, module } = await WebAssembly.instantiate(code);
        if (WebAssembly.Module.imports(module).length) throw Error(label + " has runtime imports");
        const value = String(instance.exports.run());
        if (value !== test.wasm) throw Error(label + " returned " + value + ", the CLR returned " + test.clr);
        results.push({ name: test.name, pack: test.pack, value, bytes: code.length });
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
    const values = node.filter(result => result.value !== undefined).length;
    const diagnostics = node.length - values;
    const packs = oracle.packs.map(({ name, nativeWidth, machine, magic, corFlags, version, supported, directory }) =>
        ({ name, nativeWidth, machine, magic, corFlags, version, supported, directory }));
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(),
        targetNativeWidth: 4, packs, results: node.map(({ bytes, ...result }) => result)
    }, null, 2) + "\n");
    for (const pack of packs)
        console.log(`${pack.name}: CoreLib ${pack.version}, native width ${pack.nativeWidth}, ` +
            `${pack.machine} ${pack.magic} ${pack.corFlags}, ${pack.supported ? "selected" : "rejected"}`);
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " values from the x86 CLR " + oracle.runtime + " and " + diagnostics + " native-width diagnostics.");
} finally { await browser.close(); }
