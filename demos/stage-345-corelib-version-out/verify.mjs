import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowsVersionHost } from "../stage-74-executable-entry/windows-version.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-345-corelib-version-out");
const evidence = join(root, "artifacts/stage-345-corelib-version-out/verified");
mkdirSync(evidence, { recursive: true });
const oracleFile = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oracleFile]);
const oracle = JSON.parse(readFileSync(oracleFile, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const pack = Buffer.concat([oracle.assembly, ...oracle.dependencies].flatMap(path => {
    const bytes = readFileSync(path);
    const header = Buffer.alloc(4);
    header.writeUInt32LE(bytes.length);
    return [header, bytes];
}));

async function run(compiler, pack, row) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(array, index, bytes[index]);
        return array;
    }
    const manifest = Uint8Array.of(0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110);
    new DataView(manifest.buffer).setUint32(0, row, true);
    const output = tool.CreateBytes(2 << 20);
    const diagnostic = tool.CreateDiagnostic();
    const length = tool.CompileBundleDetailed(upload(pack), upload(manifest), output, diagnostic);
    if (length <= 0)
        return { status: length, diagnostic: [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)) };

    return { bytes: length };
}

function verifyHost(createHost, version) {
    const memory = new WebAssembly.Memory({ initial: 1 });
    const query = createHost(memory, { major: version[0], minor: version[1], build: version[2] });
    const address = 128;
    const data = new DataView(memory.buffer);
    const servicePack = new Uint8Array(memory.buffer, address + 20, 256);
    const nullStatus = query.rtlGetVersion(0);
    const shortStatus = query.rtlGetVersion(memory.buffer.byteLength - 10);
    const wrongSizeStatus = query.rtlGetVersion(address);
    data.setUint32(address, 276, true);
    servicePack.fill(255);
    const status = query.rtlGetVersion(address);
    return { status, nullStatus, shortStatus, wrongSizeStatus,
        fields: [0, 4, 8, 12, 16].map(offset => data.getUint32(address + offset, true)),
        emptyServicePack: servicePack.every(unit => unit === 0) };
}

function name(value) {
    const bytes = [...new TextEncoder().encode(value)];
    return [bytes.length, ...bytes];
}

function launcherProbe(version) {
    const imports = [2, ...name("standardwasm:resources"), ...name("memory"), 2, 0, 1,
        ...name("standardwasm:windows-version"), ...name("rtlGetVersion"), 0, 0];
    const exports = [2, ...name("run"), 0, 0, ...name("memory"), 2, 0];
    const bytes = Uint8Array.of(0, 97, 115, 109, 1, 0, 0, 0,
        1, 6, 1, 0x60, 1, 0x7f, 1, 0x7f, 2, imports.length, ...imports, 7, exports.length, ...exports);
    const module = new WebAssembly.Module(bytes);
    return instantiateManagedModule(module, {}, [], {}, undefined, undefined, undefined, {
        windowsVersion: { major: version[0], minor: version[1], build: version[2] }
    });
}

const node = await run(compiler, pack, oracle.row);
const host = verifyHost(createWindowsVersionHost, oracle.version);
const launched = await launcherProbe(oracle.version);
const probeView = new DataView(launched.exports.memory.buffer);
probeView.setUint32(128, 276, true);
assert.equal(launched.exports.run(128), 0);
assert.equal(probeView.getUint32(140, true), oracle.expected);
const server = await serveLaunch(0, compilerPath);
try {
    const response = await fetch(new URL("/launch/windows-version.mjs", server.url));
    assert.equal(response.status, 200);
    assert.match(await response.text(), /function createWindowsVersionHost/);
} finally {
    await server.close();
}
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const key = new URL(route.request().url()).pathname;
        if (key === "/windows-version.mjs")
            return route.fulfill({ body: readFileSync(join(root, "demos/stage-74-executable-entry/windows-version.mjs")), contentType: "text/javascript" });
        const body = key === "/compiler" ? compiler : key === "/pack" ? pack : undefined;
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, hostSource, row, version }) => {
        const files = await Promise.all(["/compiler", "/pack"].map(async path =>
            new Uint8Array(await (await fetch(path)).arrayBuffer())));
        const { createWindowsVersionHost } = await import("/windows-version.mjs");
        return { result: await (0, eval)("(" + source + ")")(...files, row),
            host: (0, eval)("(" + hostSource + ")")(createWindowsVersionHost, version) };
    }, { source: run.toString(), hostSource: verifyHost.toString(), row: oracle.row, version: oracle.version });
    const result = { compiler: createHash("sha256").update(compiler).digest("hex"),
        clr: oracle.runtime, node: process.version, edge: browser.version(), nodeResult: node, edgeResult: edge.result, host, windowsBuild: oracle.expected };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    assert.deepEqual(edge.result, node);
    assert.deepEqual(node, { status: -2, diagnostic: [3, 15148, -1] });
    assert.deepEqual(edge.host, host);
    const invalidParameter = 0xc000000d | 0;
    assert.deepEqual(host, { status: 0, nullStatus: invalidParameter, shortStatus: invalidParameter,
        wrongSizeStatus: invalidParameter, fields: [276, ...oracle.version], emptyServicePack: true });
    console.log(JSON.stringify(result));
} finally {
    await browser.close();
}
