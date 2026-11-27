import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { createDynamicLibraryHost } from "../stage-74-executable-entry/dynamic-library-host.mjs";

const stage = join(root, "demos/stage-165-dynamic-native-calls");
const evidence = join(root, "artifacts/stage-165-dynamic-native-calls");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
assert.ok(oracle.callOffset >= 0);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const assembly = readFileSync(oracle.assembly);

async function exercise(compilerBytes, assemblyBytes, { row, unsupportedRow, pinnedEntryRow }, createRegistry) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compilerBytes);
    function upload(bytes) {
        const managed = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(managed, i, bytes[i]);
        return managed;
    }
    const image = upload(new Uint8Array(assemblyBytes));
    const output = tool.CreateBytes(64 << 10);
    const diagnostic = tool.CreateDiagnostic();
    const reject = tool.CompileDetailed(image, unsupportedRow, output, diagnostic);
    const location = [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const pinnedReject = tool.CompileDetailed(image, pinnedEntryRow, output, diagnostic);
    const pinnedLocation = [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const length = tool.CompileDetailed(image, row, output, diagnostic);
    if (length <= 0) throw Error("Unexpected compile result " + length + " at " +
        [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)));
    const bytes = Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i));
    const module = await WebAssembly.compile(bytes);
    const imports = WebAssembly.Module.imports(module).map(item => [item.module, item.name, item.kind]);
    const memory = new WebAssembly.Memory({ initial: 1 });
    const buffer = new DataView(memory.buffer);
    const address = 128;
    buffer.setUint16(address, 0x03a9, true);
    buffer.setUint16(address + 2, 0xffee, true);
    const signature = "platform:u32(i32,p32)";
    const library = createRegistry({ system: 0, pinvoke: 0 });
    library.registerLibrary("fixture.dll", new Map([
        ["Write", { signature, invoke(input, pointer) {
            if (pointer === 0) return 0xffffffff;
            buffer.setUint16(pointer, buffer.getUint16(pointer, true) + input, true);
            return (input + 0xf1234567) >>> 0;
        } }],
        ["Other", { signature, invoke: (input, pointer) => (buffer.getUint16(pointer, true) - input) >>> 0 }]
    ]));
    const handle = library.loadLibrary("fixture.dll");
    const write = library.getProcAddress(handle, "Write");
    const other = library.getProcAddress(handle, "Other");
    const { exports } = await WebAssembly.instantiate(module, {
        "standardwasm:resources": { memory },
        "standardwasm:native-indirect": { [signature]: (token, input, pointer) => library.invoke(token, signature, input, pointer) }
    });
    const first = exports.run(write, -7, address) >>> 0;
    const second = exports.run(other, 17, address) >>> 0;
    const nullPointer = exports.run(write, 5, 0) >>> 0;
    library.freeLibrary(handle);
    let staleRejected = false;
    try { exports.run(write, -7, address); }
    catch (error) { staleRejected = /unregistered dynamic export token/.test(error.message); }
    return { first, second, nullPointer, units: [buffer.getUint16(address, true), buffer.getUint16(address + 2, true)],
        staleRejected, reject, location, pinnedReject, pinnedLocation, imports };
}

assert.equal(oracle.pinnedResult, 0xf1234568);
const rows = { row: oracle.row, unsupportedRow: oracle.unsupportedRow, pinnedEntryRow: oracle.pinnedEntryRow };
const result = await exercise(compiler, assembly, rows, createDynamicLibraryHost);
assert.deepEqual(result, {
    first: oracle.first, second: oracle.second, nullPointer: oracle.nullPointer, units: oracle.units, staleRejected: true,
    reject: -2, location: [0, oracle.unsupportedRow, oracle.callOffset],
    pinnedReject: -2, pinnedLocation: [0, oracle.pinnedRow, 1],
    imports: [["standardwasm:resources", "memory", "memory"],
        ["standardwasm:native-indirect", "platform:u32(i32,p32)", "function"]]
});

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = {
        "/": ["text/html", "<!doctype html>"],
        "/compiler": ["application/wasm", compiler],
        "/assembly": ["application/octet-stream", assembly],
        "/registry.mjs": ["text/javascript", readFileSync(join(stage, "../stage-74-executable-entry/dynamic-library-host.mjs"))]
    };
    await page.route("http://127.0.0.1/**", route => {
        const [contentType, body] = assets[new URL(route.request().url()).pathname];
        return route.fulfill({ contentType, body });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, rows }) => {
        const { createDynamicLibraryHost } = await import("/registry.mjs");
        const [wasm, image] = await Promise.all(["/compiler", "/assembly"]
            .map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(wasm, image, rows, createDynamicLibraryHost);
    }, { source: exercise.toString(), rows });
    assert.deepEqual(edge, result);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(assembly), clr: oracle.runtime,
        clrArchitecture: oracle.architecture, node: process.version, edge: browser.version(), result };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify(report));
} finally {
    await browser.close();
}
