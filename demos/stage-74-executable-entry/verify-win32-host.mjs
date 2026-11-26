import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";
import { instantiateManagedModule } from "./resource-host.mjs";

const methods = [
    ["getModuleHandleW", 0], ["getLastSystemError", 1], ["setLastSystemError", 2],
    ["getLastPInvokeError", 1], ["setLastPInvokeError", 2]
];

function unsigned(value) {
    const bytes = [];
    while (value >= 128) {
        bytes.push((value & 127) | 128);
        value >>>= 7;
    }
    return [...bytes, value];
}

const nameBytes = name => {
    const bytes = new TextEncoder().encode(name);
    return [...unsigned(bytes.length), ...bytes];
};
const section = (id, payload) => [id, ...unsigned(payload.length), ...payload];

function probeModule() {
    const imports = [
        ...nameBytes("standardwasm:resources"), ...nameBytes("memory"), 2, 0, 1,
        ...methods.flatMap(([name, type]) => [...nameBytes("standardwasm:win32"), ...nameBytes(name), 0, type])
    ];
    const exports = [
        ...nameBytes("memory"), 2, 0,
        ...methods.flatMap(([name], index) => [...nameBytes(name), 0, index])
    ];
    return Uint8Array.from([
        0, 97, 115, 109, 1, 0, 0, 0,
        ...section(1, [3, 0x60, 1, 0x7f, 1, 0x7f, 0x60, 0, 1, 0x7f, 0x60, 1, 0x7f, 0]),
        ...section(2, [methods.length + 1, ...imports]),
        ...section(7, [methods.length + 1, ...exports])
    ]);
}

async function exercise(instantiate, bytes, executable) {
    const module = await WebAssembly.compile(new Uint8Array(bytes));
    const make = () => instantiate(module, {}, [], {}, "apps/" + executable);
    const { exports: host } = await make();

    function writeName(address, text) {
        const view = new DataView(host.memory.buffer);
        for (let i = 0; i < text.length; i++) view.setUint16(address + i * 2, text.charCodeAt(i), true);
        view.setUint16(address + text.length * 2, 0, true);
        return address;
    }

    const processHandle = host.getModuleHandleW(0);
    const results = [];
    for (const name of [null, executable.toUpperCase(), "missing-standardwasm-native-module.dll", "", executable + "\0suffix"]) {
        host.setLastSystemError(0);
        const handle = host.getModuleHandleW(name === null ? 0 : writeName(32, name));
        host.setLastPInvokeError(host.getLastSystemError());
        results.push([handle === 0 ? 0 : handle === processHandle ? 1 : -1, host.getLastPInvokeError()]);
    }
    host.setLastPInvokeError(73);
    host.setLastSystemError(19);
    const errors = [host.getLastSystemError(), host.getLastPInvokeError()];
    const second = (await make()).exports;
    const isolated = [second.getLastSystemError(), second.getLastPInvokeError(), second.memory !== host.memory];

    const addr = host.memory.grow(1) * 65536;
    const afterGrowth = host.getModuleHandleW(writeName(addr, "APPS/" + executable.toUpperCase()));
    const otherPath = host.getModuleHandleW(writeName(32, "other/" + executable));
    const pathError = host.getLastSystemError();
    const invalidAddress = host.getModuleHandleW(host.memory.buffer.byteLength - 1);
    return { results, errors, isolated, pathMatches: afterGrowth === processHandle,
        otherPath, pathError, invalidAddress, pointerError: host.getLastSystemError(), nonzeroProcessHandle: processHandle !== 0 };
}

async function exerciseCompiler(instantiate, compilerBytes, fixtureBytes) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(new Uint8Array(compilerBytes));
    function upload(bytes) {
        const result = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(result, i, bytes[i]);
        return result;
    }

    const fixture = upload(fixtureBytes);
    const encoder = new TextEncoder();
    const names = ["Exchange", "Read"].map(name => encoder.encode(name));
    const manifest = new Uint8Array(names.reduce((size, name) => size + 8 + name.length, 0));
    const view = new DataView(manifest.buffer);
    let offset = 0;
    for (const name of names) {
        const row = compiler.FindRow(fixture, upload(name));
        if (row <= 0) throw Error("Missing Marshal fixture export");
        view.setInt32(offset, row, true);
        view.setInt32(offset + 4, name.length, true);
        manifest.set(name, offset + 8);
        offset += 8 + name.length;
    }

    const output = compiler.CreateBytes(65536);
    const location = compiler.CreateDiagnostic();
    const length = compiler.CompileModuleDetailed(fixture, upload(manifest), output, location);
    if (length <= 0) throw Error("Marshal fixture failed to compile at " +
        [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index)));
    const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    const { exports } = await instantiate(await WebAssembly.compile(code), compiler, []);
    return [exports.Exchange(19), exports.Exchange(-13), exports.Read()];
}

const oracle = JSON.parse(execFileSync("dotnet", ["run", "--file",
    fileURLToPath(new URL("./Win32HostOracle.cs", import.meta.url))], { encoding: "utf8" }));
const compilerPath = process.argv[2];
let compilerBytes, fixtureBytes;
if (compilerPath) {
    execFileSync("dotnet", ["build", fileURLToPath(new URL("./Win32Fixture", import.meta.url)), "-c", "Release"],
        { stdio: "inherit" });
    compilerBytes = readFileSync(compilerPath);
    fixtureBytes = readFileSync(new URL("./Win32Fixture/bin/Release/net10.0/Win32Fixture.dll", import.meta.url));
    assert.deepEqual(await exerciseCompiler(instantiateManagedModule, compilerBytes, fixtureBytes), oracle.compiledFixture);
}
const bytes = probeModule();
const node = await exercise(instantiateManagedModule, bytes, oracle.executable);
assert.deepEqual(node, { results: oracle.results, errors: oracle.errors,
    isolated: [0, 0, true], pathMatches: true, otherPath: 0, pathError: 126,
    invalidAddress: 0, pointerError: 998, nonzeroProcessHandle: true });

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const base = "http://127.0.0.1";
    await page.route(base + "/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        if (path === "/resource-host.mjs" || path === "/win32-host.mjs")
            return route.fulfill({ contentType: "text/javascript", body: readFileSync(new URL("." + path, import.meta.url)) });
        return route.abort();
    });
    await page.goto(base);
    const edge = await page.evaluate(async ({ source, bytes, executable }) => {
        const { instantiateManagedModule } = await import("/resource-host.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, bytes, executable);
    }, { source: exercise.toString(), bytes: [...bytes], executable: oracle.executable });
    assert.deepEqual(edge, node);
    if (compilerBytes) {
        const compiled = await page.evaluate(async ({ source, compilerBytes, fixtureBytes }) => {
            const { instantiateManagedModule } = await import("/resource-host.mjs");
            return (0, eval)("(" + source + ")")(instantiateManagedModule, compilerBytes, fixtureBytes);
        }, { source: exerciseCompiler.toString(), compilerBytes: [...compilerBytes], fixtureBytes: [...fixtureBytes] });
        assert.deepEqual(compiled, oracle.compiledFixture);
        console.log("The self-hosted compiler matched the CLR Marshal error exchange in Node and Edge.");
    }
    console.log("Node and Edge matched 5 CLR module lookups and both last-error slots. Memory growth and host isolation passed.");
} finally {
    await browser.close();
}
