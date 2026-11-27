import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { instantiateManagedModule } from "./resource-host.mjs";
import { createWindowState } from "./window-state.mjs";
import { serveLaunch } from "./launch-server.mjs";

const stage = join(root, "demos", "stage-74-executable-entry");
const artifacts = join(root, "artifacts", "window-proc-lookup");
mkdirSync(artifacts, { recursive: true });
const evidence = mkdtempSync(join(artifacts, "run-"));
const oraclePath = join(evidence, "oracle.json");
const project = join(stage, "WindowProcOracle");
runOn32BitClr(wasm32Runtime(), project, join(project, "bin", "Release", "net10.0-windows", "WindowProcOracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
assert.deepEqual(oracle.original, { matches: true, cached: true });

function fixture(includeSymbol) {
    const i32 = 0x7f;
    const types = [[0x60, 1, i32, 1, i32], [0x60, 2, i32, i32, 1, i32],
        [0x60, 0, 1, i32], [0x60, 1, i32, 0]];
    const calls = [["getModuleHandleW", 0], ["getLastSystemError", 2], ["getLastPInvokeError", 2],
        ["setLastPInvokeError", 3]];
    if (includeSymbol) calls.push(["getProcAddress", 1], ["getWindowLongPtrW", 1]);
    const name = value => [value.length, ...Buffer.from(value)];
    const section = (id, bytes) => bytes.length < 128
        ? [id, bytes.length, ...bytes]
        : [id, (bytes.length & 127) | 128, bytes.length >>> 7, ...bytes];
    const imports = calls.map(([entry, type]) => [...name("standardwasm:win32"), ...name(entry), 0, type]);
    imports.unshift([...name("standardwasm:resources"), ...name("memory"), 2, 0, 1]);
    const exports = calls.map(([entry], index) => [...name(entry), 0, index]);
    exports.push([...name("memory"), 2, 0]);
    return [0, 97, 115, 109, 1, 0, 0, 0,
        ...section(1, [types.length, ...types.flat()]),
        ...section(2, [imports.length, ...imports.flat()]),
        ...section(7, [exports.length, ...exports.flat()])
    ];
}

async function exercise(instantiate, createWindows, variants) {
    const windows = createWindows();
    const module = new WebAssembly.Module(Uint8Array.from(variants.full));
    const lookupOnly = new WebAssembly.Module(Uint8Array.from(variants.module));
    const start = (wasm, platform) => instantiate(wasm, {}, [], {}, "reference.exe", windows, undefined, platform);
    let expectedModule, expectedProcedure;
    const platform = {
        configureNativeLibraries({ registerLibrary, loadLibrary, getProcAddress }) {
            registerLibrary("user32", new Map([
                ["DefWindowProcW", { signature: "stdcall:i32(p32,u32,u32,i32)", invoke() {
                    throw new Error("This registry fixture exercises symbol lookup only");
                } }]
            ]));
            expectedModule = loadLibrary("user32.dll", 0, 0x800);
            expectedProcedure = getProcAddress(expectedModule, "DefWindowProcW");
        }
    };

    function populate(memory) {
        const base = memory.grow(1) * 65536;
        const view = new DataView(memory.buffer, base);
        function string(at, text, wide) {
            for (let i = 0; i <= text.length; i++) {
                const unit = i === text.length ? 0 : text.charCodeAt(i);
                if (wide) view.setUint16(at + i * 2, unit, true);
                else view.setUint8(at + i, unit);
            }
            return base + at;
        }
        return {
            user32: string(0, "UsEr32", true),
            missing: string(64, "StandardWasm-Missing-Window-Fixture.dll", true),
            procedure: string(256, "DefWindowProcW", false),
            wrongCase: string(288, "defwindowprocw", false),
            path: string(320, "C:\\Windows\\System32\\user32.dll", true),
            executable: string(448, "reference.exe", true)
        };
    }

    const { exports: guest } = await start(module, platform);
    const addresses = populate(guest.memory);
    const h = guest.getModuleHandleW(addresses.user32);
    const p = guest.getProcAddress(h, addresses.procedure);
    const missingModule = guest.getModuleHandleW(addresses.missing);
    const moduleError = guest.getLastSystemError();
    const missingProcedure = guest.getProcAddress(h, addresses.wrongCase);
    const procedureError = guest.getLastSystemError();
    const lookup = { sameModule: h === expectedModule, sameProcedure: p === expectedProcedure,
        missingModule, moduleError, missingProcedure, procedureError };
    const hwnd = windows.register(0, undefined, { procedure: p });
    const storedProcedure = guest.getWindowLongPtrW(hwnd, -4) === p;
    const executable = [guest.getModuleHandleW(0), guest.getModuleHandleW(addresses.executable)];
    const diskPath = guest.getModuleHandleW(addresses.path);
    const diskError = guest.getLastSystemError();

    guest.setLastPInvokeError(321);
    const ordinal = guest.getProcAddress(h, 16);
    const ordinalError = guest.getLastSystemError();
    const buffer = new Uint8Array(guest.memory.buffer);
    buffer[buffer.length - 1] = 65;
    const malformed = guest.getProcAddress(h, buffer.length - 1);
    const malformedError = guest.getLastSystemError();
    const preservedError = guest.getLastPInvokeError();

    const { exports: second } = await start(module, platform);
    const secondAddresses = populate(second.memory);
    const fresh = second.getModuleHandleW(secondAddresses.user32) !== h;
    const foreign = second.getProcAddress(h, secondAddresses.procedure);
    const foreignError = second.getLastSystemError();

    const { exports: empty } = await start(module);
    const noProvider = empty.getModuleHandleW(populate(empty.memory).user32);
    const noProviderError = empty.getLastSystemError();
    const { exports: names } = await start(lookupOnly, platform);
    const namesOnly = names.getModuleHandleW(populate(names.memory).user32) === expectedModule;
    return { lookup, storedProcedure, executable, diskPath, diskError, ordinal, ordinalError, malformed,
        malformedError, preservedError, fresh, foreign, foreignError, noProvider, noProviderError, namesOnly };
}

async function exerciseCompiled(instantiate, compilerBytes, assemblyBytes, row) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compilerBytes);
    const image = tool.CreateBytes(assemblyBytes.byteLength);
    new Uint8Array(assemblyBytes).forEach((value, index) => tool.SetByte(image, index, value));
    const destination = tool.CreateBytes(65536);
    const diagnostic = tool.CreateDiagnostic();
    const size = tool.CompileDetailed(image, row, destination, diagnostic);
    if (size <= 0) throw Error("Window procedure fixture status " + size + " at " +
        [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(destination, index)));
    let library, token, memory;
    const { exports } = await instantiate(module, tool, [], {}, undefined, undefined, undefined, {
        configureNativeLibraries({ memory: guestMemory, registerLibrary, loadLibrary, getProcAddress }) {
            registerLibrary("user32", new Map([
                ["DefWindowProcW", { signature: "stdcall:i32(p32,u32,u32,i32)", invoke() { throw Error("lookup only"); } }]
            ]));
            library = loadLibrary("user32");
            token = getProcAddress(library, "DefWindowProcW");
            memory = guestMemory;
        }
    });
    const address = memory.grow(1) * 65536;
    new Uint8Array(memory.buffer, address).set(new TextEncoder().encode("DefWindowProcW\0"));
    const found = exports.run(library, address) === token;
    new Uint8Array(memory.buffer, address)[0] = 0x64;
    const missing = exports.run(library, address);
    const imports = WebAssembly.Module.imports(module).map(({ module, name }) => module + "/" + name);
    return { found, missing, imports };
}

const variants = { full: fixture(true), module: fixture(false) };
const node = await exercise(instantiateManagedModule, createWindowState, variants);
assert.deepEqual(node, {
    lookup: oracle.lookup, storedProcedure: true, executable: [1, 1], diskPath: 0, diskError: 126, ordinal: 0,
    ordinalError: 127, malformed: 0, malformedError: 998, preservedError: 321, fresh: true, foreign: 0,
    foreignError: 6, noProvider: 0, noProviderError: 126, namesOnly: true
});

const compiler = process.argv[2] ? readFileSync(process.argv[2]) : null;
const fixtureAssembly = compiler ? readFileSync(oracle.fixtureAssembly) : null;
const compiled = compiler ? await exerciseCompiled(instantiateManagedModule, compiler, fixtureAssembly, oracle.fixtureRow) : null;
if (compiled) assert.deepEqual(compiled, {
    found: oracle.nominal, missing: 0,
    imports: ["standardwasm:resources/memory", "standardwasm:win32/getProcAddress"]
});

const server = await serveLaunch(0, new Uint8Array());
const edge = await chromium.launch({ channel: "msedge" });
try {
    const page = await edge.newPage();
    if (compiled) {
        await page.route(server.url + "/compiler", route => route.fulfill({ body: compiler }));
        await page.route(server.url + "/fixture", route => route.fulfill({ body: fixtureAssembly }));
    }
    await page.goto(server.url);
    const { browser, edgeCompiled } = await page.evaluate(async ({ source, compiledSource, variants, row }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const browser = await (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, variants);
        const assets = compiledSource ? await Promise.all(["/compiler", "/fixture"]
            .map(async url => (await fetch(url)).arrayBuffer())) : null;
        const edgeCompiled = assets ? await (0, eval)("(" + compiledSource + ")")(instantiateManagedModule, ...assets, row) : null;
        return { browser, edgeCompiled };
    }, { source: exercise.toString(), compiledSource: compiled ? exerciseCompiled.toString() : null,
        variants, row: oracle.fixtureRow });
    assert.deepEqual(browser, node);
    assert.deepEqual(edgeCompiled, compiled);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, browser, compiled, nodeVersion: process.version, edgeVersion: edge.version()
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 module/export lookup and checked browser registry isolation.");
    if (compiled) console.log("Compiled HMODULE/PCSTR/FARPROC lookup matched the Windows x86 result in both engines.");
} finally {
    await edge.close();
    await server.close();
}
