import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import * as locale from "../stage-74-executable-entry/mono-locale-host.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos/stage-336-mono-icu-locale");
const artifacts = join(root, "artifacts/stage-336-mono-icu-locale");
mkdirSync(artifacts, { recursive: true });
const runtime = wasm32Runtime();
const native = process.env.STANDARDWASM_MONO_NATIVE || join(process.env.USERPROFILE, ".nuget/packages",
    "microsoft.netcore.app.runtime.mono.browser-wasm", runtime.version, "runtimes/browser-wasm/native");
const core = join(native, "System.Private.CoreLib.dll");
const oraclePath = join(artifacts, "oracle.json");
runOn32BitClr(runtime, join(stage, "Oracle/Oracle.csproj"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"),
    [oraclePath, core]);
const expected = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const assets = Object.fromEntries(Object.entries({
    compiler: compilerPath,
    core,
    fixture: expected.assembly,
    sidecar: process.env.STANDARDWASM_ICU_SIDECAR || join(root, "artifacts/stage336-icu.wasm"),
    efigs: join(native, "icudt_EFIGS.dat"),
    full: join(native, "icudt.dat")
}).map(([name, path]) => [name, readFileSync(path)]));

async function exercise(assets, oracle, host, locale) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(assets.compiler);
    function upload(bytes) {
        const input = compiler.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) compiler.SetByte(input, index, bytes[index]);
        return input;
    }
    function compile(image, row, bundle) {
        const header = new Uint8Array([0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110]);
        new DataView(header.buffer).setUint32(0, row, true);
        const output = compiler.CreateBytes(2 << 20);
        const diagnostic = compiler.CreateDiagnostic();
        const compile = bundle ? compiler.CompileBundleDetailed : compiler.CompileModuleDetailed;
        const length = compile(upload(new Uint8Array(image)), upload(header), output, diagnostic);
        if (length <= 0)
            throw Error("Compile returned " + length + " at " + [0, 1, 2].map(i => compiler.ReadDiagnostic(diagnostic, i)));
        return new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i)));
    }

    // Root the untouched original CoreLib wrapper. Its own pin and native
    // thunk execute against the ordinary executable import resolver.
    const pack = new Uint8Array(4 + assets.core.byteLength);
    new DataView(pack.buffer).setUint32(0, assets.core.byteLength, true);
    pack.set(new Uint8Array(assets.core), 4);
    const wrapper = compile(pack, oracle.wrapper, true);
    const text = new WebAssembly.Instance(compile(assets.fixture, oracle.row, false));
    const sidecar = new WebAssembly.Module(assets.sidecar);
    const profile = data => locale.createMonoLocaleIcuProvider(sidecar, new Uint8Array(data));
    const attach = monoLocaleIcu => host(wrapper, compiler, [], {}, undefined, undefined, undefined, { monoLocaleIcu });
    const efigsProvider = profile(assets.efigs);
    const efigs = await attach(efigsProvider);
    const full = await attach(profile(assets.full));
    const outcomes = [...Array(16).keys()].map(index => efigs.exports.run(text.exports.run(index)));
    const fullProfile = [...Array(16).keys()].map(index => full.exports.run(text.exports.run(index)));

    const absent = await attach(undefined);
    let missing = false;
    try { absent.exports.run(text.exports.run(2)); }
    catch (error) {
        if (!(error instanceof WebAssembly.Exception) || !error.is(absent.exports["\0object"])) throw error;
        missing = true;
    }

    const memory = new WebAssembly.Memory({ initial: 1 });
    const lookup = locale.bindMonoLocale(memory, efigsProvider);
    const dataView = new DataView(memory.buffer);
    const end = dataView.byteLength - 2;
    const rejected = [0, 3, -2].map(pointer => {
        try { lookup({}, pointer); }
        catch (error) { if (error instanceof WebAssembly.RuntimeError) return true; throw error; }
        return false;
    });
    const lastEmpty = lookup({}, end);
    dataView.setUint16(end, 65, true);
    try { lookup({}, end); rejected.push(false); }
    catch (error) { if (!(error instanceof WebAssembly.RuntimeError)) throw error; rejected.push(true); }
    return { outcomes, fullProfile, missing, lastEmpty, rejected, preservedProfile: efigs.exports.run(text.exports.run(6)) };
}

const node = await exercise(assets, expected, instantiateManagedModule, locale);
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route(server.url + "/test-assets/**", route => {
        const name = new URL(route.request().url()).pathname.slice("/test-assets/".length);
        const body = Object.hasOwn(assets, name) ? assets[name] : null;
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) : route.fulfill({ status: 404 });
    });
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, names, oracle }) => {
        const host = await import("/launch/resource-host.mjs");
        const locale = await import("/launch/mono-locale-host.mjs");
        const downloaded = await Promise.all(names.map(async name => {
            const response = await fetch("/test-assets/" + name);
            if (!response.ok) throw Error("Missing test asset " + name);
            return [name, await response.arrayBuffer()];
        }));
        return (0, eval)("(" + source + ")")(Object.fromEntries(downloaded), oracle, host.instantiateManagedModule, locale);
    }, { source: exercise.toString(), names: Object.keys(assets), oracle: expected });
    const stockEfigs = [1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 0, 1, 1, 0];
    assert.deepEqual(edge, node);
    assert.deepEqual(node.outcomes, stockEfigs);
    assert.deepEqual(node.fullProfile, stockEfigs.map((value, index) => index === 6 ? 1 : value));
    for (const check of expected.clr) assert.equal(node.outcomes[check.index], check.result);
    assert.equal(node.missing, true);
    assert.equal(node.lastEmpty, 1);
    assert.deepEqual(node.rejected, [true, true, true, true]);
    assert.equal(node.preservedProfile, 0);
    const hashes = Object.fromEntries(Object.entries(assets).map(([key, bytes]) => [key, createHash("sha256").update(bytes).digest("hex")]));
    const evidence = { hashes, clr: expected.runtime, node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(artifacts, "results.json"), JSON.stringify(evidence, null, 2) + "\n");
    console.log(JSON.stringify(evidence));
} finally {
    await browser.close();
    await server.close();
}
