import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createTempPathHost } from "../stage-74-executable-entry/temp-path-host.mjs";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-165-dynamic-native-calls");
const evidence = join(root, "artifacts/stage-165-dynamic-native-calls");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
assert.ok(oracle.callOffset >= 0);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const assembly = readFileSync(oracle.assembly);
const framework = ["System.Private.CoreLib.dll", "System.Runtime.dll", "System.Runtime.InteropServices.dll", "netstandard.dll"]
    .map(path => ({ path, bytes: readFileSync(join(runtime.directory, path)) }));

async function exercise(compilerBytes, assemblyBytes, { row, unsupportedRow, pinnedEntryRow, arrayRow, stackRow }, instantiate) {
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
    const manifest = [];
    for (const [method, name] of [[row, "run"], [arrayRow, "pinnedArray"], [stackRow, "pinnedStack"]]) {
        const bytes = new TextEncoder().encode(name);
        manifest.push(method & 255, method >>> 8 & 255, method >>> 16 & 255, 0, bytes.length, 0, 0, 0, ...bytes);
    }
    const length = tool.CompileModuleDetailed(image, upload(manifest), output, diagnostic);
    if (length <= 0) throw Error("Unexpected compile result " + length + " at " +
        [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)));
    const bytes = Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i));
    const module = await WebAssembly.compile(bytes);
    const imports = WebAssembly.Module.imports(module).map(item => [item.module, item.name, item.kind]);
    let memory, write, other, handle, free;
    const extents = [];
    const address = 128;
    const signature = "platform:u32(i32,p32)";
    const { exports } = await instantiate(module, tool, [], {}, undefined, undefined, undefined, {
        configureNativeLibraries({ memory: guestMemory, registerLibrary, loadLibrary, getProcAddress, freeLibrary }) {
            memory = guestMemory;
            const buffer = new DataView(memory.buffer);
            buffer.setUint16(address, 0x03a9, true);
            buffer.setUint16(address + 2, 0xffee, true);
            function writeUnit(input, pointer) {
                if (pointer === 0) return 0xffffffff;
                const view = new DataView(memory.buffer);
                view.setUint16(pointer, view.getUint16(pointer, true) + input, true);
                return (input + 0xf1234567) >>> 0;
            }
            registerLibrary("fixture.dll", new Map([
                ["Write", { signature, invoke: writeUnit, scopedUtf16(input, pointer, available) {
                    if (available === 0) throw new RangeError("Missing UTF-16 code unit");
                    extents.push(available);
                    return writeUnit(input, pointer);
                } }],
                ["Other", { signature, invoke: (input, pointer) => (new DataView(memory.buffer).getUint16(pointer, true) - input) >>> 0 }]
            ]));
            handle = loadLibrary("fixture.dll");
            write = getProcAddress(handle, "Write");
            other = getProcAddress(handle, "Other");
            free = freeLibrary;
        }
    });
    const first = exports.run(write, -7, address) >>> 0;
    const second = exports.run(other, 17, address) >>> 0;
    const nullPointer = exports.run(write, 5, 0) >>> 0;
    const pinnedArray = exports.pinnedArray(write, 1) >>> 0;
    const pinnedStack = exports.pinnedStack(write, 1) >>> 0;
    let unconfiguredScopeRejected = false;
    try { exports.pinnedArray(other, 1); }
    catch (error) { unconfiguredScopeRejected = /does not accept scoped UTF-16/.test(error.message); }
    const retry = exports.pinnedArray(write, 1) >>> 0;
    const buffer = new DataView(memory.buffer);
    free(handle);
    let staleRejected = false;
    try { exports.run(write, -7, address); }
    catch (error) { staleRejected = /unregistered dynamic export token/.test(error.message); }
    return { first, second, nullPointer, units: [buffer.getUint16(address, true), buffer.getUint16(address + 2, true)],
        pinnedArray, pinnedStack, extents, retry, unconfiguredScopeRejected, staleRejected, reject, location, pinnedReject, pinnedLocation, imports };
}

async function exerciseLibrary(compilerBytes, assemblyBytes, framework, oracle, pack, instantiate, createTempPath) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compilerBytes);
    const output = tool.CreateBytes(1 << 20);
    const diagnostic = tool.CreateDiagnostic();
    function upload(bytes) {
        const managed = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(managed, index, bytes[index]);
        return managed;
    }
    const asset = { path: "DynamicCallFixture.dll", bytes: new Uint8Array(assemblyBytes) };
    const origins = tool.BundleModuleOrigins?.() === 1;
    async function compile(row, entry = asset, companions = framework) {
        const [bundle, entries] = pack(entry, companions, row, origins);
        const size = tool.CompileBundleDetailed(upload(bundle), upload(entries), output, diagnostic);
        if (size <= 0) throw Error("LoadLibraryExW fixture compile result " + size + " at " +
            [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)));
        const compiled = Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index));
        return WebAssembly.compile(compiled);
    }

    const module = await compile(oracle.loadRow);
    const symbolModule = await compile(oracle.symbolRow);
    const symbolNameModule = await compile(oracle.symbolNameRow);
    const originalSymbolModule = await compile(oracle.originalSymbolRow, framework[0], framework.slice(1));
    const tryLookupModule = await compile(oracle.trySymbolRow);
    const nativeWordModule = await compile(oracle.nativeWordRow, asset, []);
    if (WebAssembly.Module.imports(nativeWordModule).length)
        throw Error("Managed native-word locations must not require a linear-memory host");
    const { exports: nativeWord } = await instantiate(nativeWordModule, tool, []);
    const nativeWordAliases = nativeWord.run(7);
    const sourceName = await compile(oracle.nameRow);
    const combined = await compile(oracle.combinedRow);
    const coreLib = await compile(oracle.coreLibRow, framework[0], framework.slice(1));
    const signature = "platform:u32(i32,p32)";
    let memory, library, token;
    const platform = {
        configureNativeLibraries({ memory: guestMemory, errors, registerLibrary, loadLibrary, getProcAddress }) {
            memory = guestMemory;
            const temp = createTempPath(memory, oracle.tempPath, errors);
            registerLibrary("kernel32", new Map([
                ["GetTempPathW", { signature, invoke: temp.getTempPathW }]
            ]));
            library = loadLibrary("Kernel32");
            token = getProcAddress(library, "GetTempPathW");
        }
    };

    const unconfigured = await instantiate(module, tool, []);
    const unconfiguredResult = [unconfigured.exports.run(0), unconfigured.exports.run(2)];
    const { exports: load } = await instantiate(module, tool, [], {}, undefined, undefined, undefined, platform);
    const firstLibrary = library;
    const { exports: symbolFixture } = await instantiate(symbolModule, tool, [], {}, undefined, undefined, undefined, platform);
    const symbols = [0, 1].map(scenario => symbolFixture.run(scenario));
    const { exports: tryLookup } = await instantiate(tryLookupModule, tool, [], {}, undefined, undefined, undefined, platform);
    const trySymbols = [0, 1].map(scenario => tryLookup.run(scenario));
    const { exports: names } = await instantiate(symbolNameModule, tool, []);
    const { exports: originalLookup } = await instantiate(originalSymbolModule, tool, [], {}, undefined, undefined, undefined, platform);
    const originalSymbol = [originalLookup.run(library, names.run(0), 0) === token ? 1 : 0,
        originalLookup.run(library, names.run(1), 0)];
    const cases = [load.run(0) === firstLibrary ? 1 : 0, load.run(1), load.run(2), load.run(3), load.run(6), load.run(10)];

    const { exports: mixed } = await instantiate(combined, tool, [], {}, undefined, undefined, undefined, platform);
    const isolatedRegistry = library !== firstLibrary;
    const required = mixed.run(library, token, 0, 0);
    const address = 128;
    const length = mixed.run(library, token, required, address);
    const text = String.fromCharCode(...new Uint16Array(memory.buffer, address, required));
    const imports = WebAssembly.Module.imports(combined).map(({ module, name }) => module + "/" + name);
    const { exports: nameFixture } = await instantiate(sourceName, tool, []);
    let coreLibErrors, expectedLibrary;
    const { exports: original } = await instantiate(coreLib, tool, [], {}, undefined, undefined, undefined, {
        configureNativeLibraries({ memory, errors, registerLibrary, loadLibrary }) {
            coreLibErrors = errors;
            registerLibrary("kernel32", new Map([
                ["GetTempPathW", { signature, invoke: createTempPath(
                    memory, oracle.tempPath, errors).getTempPathW }]
            ]));
            expectedLibrary = loadLibrary("kernel32");
        }
    });
    const originalSuccess = [original.run(nameFixture.run(), 0, 0x800) === expectedLibrary ? 1 : 0, coreLibErrors.pinvoke];
    const originalNull = [original.run(null, 0, 0x800), coreLibErrors.system, coreLibErrors.pinvoke];
    return { cases, symbols, originalSymbol, trySymbols, nativeWordAliases, unconfigured: unconfiguredResult, isolatedRegistry, required, length, text, imports, originalSuccess, originalNull };
}

const nativeIndirect = "standardwasm:native-indirect";
const signature = "platform:u32(i32,p32)";
const win32 = "standardwasm:win32", helpers = "standardwasm:native";
const hiddenError = "System.Runtime.InteropServices.Marshal::GetLastPInvokeError";

function resolverProbe({ field = signature, memory = true, wrongType = false, duplicate = false } = {}) {
    const i32 = 0x7f;
    const types = [
        [0x60, 3, i32, i32, i32, 1, i32],
        [0x60, 1, i32, 1, i32],
        [0x60, 0, 1, i32],
        [0x60, 1, i32, 0],
        [0x60, 3, i32, i32, i32, 1, 0x7e]
    ];
    const functions = [
        [win32, "getLastSystemError", 2],
        [win32, "setLastPInvokeError", 3],
        [nativeIndirect, field, wrongType ? 4 : 0],
        [helpers, "present", 1],
        [helpers, "library", 1],
        ["native:kernel32.dll", "MulDiv", 0],
        ["internalcall", hiddenError, 2]
    ];
    if (duplicate) functions.push(functions[2]);
    const name = text => [text.length, ...Buffer.from(text)];
    const section = (id, payload) => {
        let bytes = payload.length;
        const size = [];
        do {
            size.push((bytes & 127) | (bytes > 127 ? 128 : 0));
            bytes >>>= 7;
        } while (bytes);
        return [id, ...size, ...payload];
    };
    const imports = functions.map(([owner, entry, type]) => [...name(owner), ...name(entry), 0, type]);
    if (memory) imports.unshift([...name("standardwasm:resources"), ...name("memory"), 2, 0, 1]);
    const exports = functions.map((_, index) =>
        [...name(index === 2 ? "invoke" : index === 6 ? "getLastPInvokeError" : "entry" + index), 0, index]);
    return [0, 97, 115, 109, 1, 0, 0, 0,
        ...section(1, [types.length, ...types.flat()]),
        ...section(2, [imports.length, ...imports.flat()]),
        ...section(7, [exports.length, ...exports.flat()])
    ];
}

async function exerciseResolver(instantiate, createTempPath, variants, signature) {
    const platformWith = configureNativeLibraries => ({ configureNativeLibraries });
    const start = (name, platform, runtimeImports = {}) => instantiate(
        new WebAssembly.Module(Uint8Array.from(variants[name])), {}, [], runtimeImports,
        undefined, undefined, undefined, platform);
    const declines = async (work, pattern) => {
        try { await work(); }
        catch (error) { return pattern.test(error.message); }
        return false;
    };
    const directory = "V:\\Scratch\\";
    let token, memory;
    const config = platformWith(({ memory: guestMemory, errors, registerLibrary, loadLibrary, getProcAddress }) => {
        memory = guestMemory;
        const provider = createTempPath(memory, directory, errors);
        registerLibrary("kernel32", new Map([
            ["GetTempPathW", { signature, invoke: provider.getTempPathW }]
        ]));
        token = getProcAddress(loadLibrary("kernel32.dll", 0, 0x800), "GetTempPathW");
    });
    const { exports: instance } = await start("mixed", config);
    const firstToken = token;
    instance.entry1(321); // fixed Win32 setLastPInvokeError
    const invalid = instance.invoke(firstToken, 1, 0);
    const system = instance.entry0(); // fixed Win32 getLastSystemError
    const saved = instance.getLastPInvokeError(); // direct InternalCall
    const required = instance.invoke(firstToken, 0, 0);
    const length = instance.invoke(firstToken, required, 8);
    const text = String.fromCharCode(...new Uint16Array(memory.buffer, 8, required));
    const direct = instance.entry5(10, 3, 2);
    const ordinals = [0, 3, 4].map(index => [instance.entry3(index), instance.entry4(index)]);

    const { exports: second } = await start("mixed", config);
    const foreign = await declines(() => second.invoke(firstToken, 0, 0), /unregistered dynamic export token/);
    const noProvider = await declines(() => start("mixed", undefined, {
        "standardwasm:native-indirect": { [signature]: () => 0 }
    }), /requires an explicit browser library provider/);
    const wrongName = await declines(() => start("wrongName", config), /invalid native indirect import/);
    const noMemory = await declines(() => start("noMemory", config), /requires the StandardWasm memory import/);
    const duplicate = await declines(() => start("duplicate", config), /invalid native indirect import/);
    const wrongType = await declines(() => start("wrongType", config), /signature|type/i);
    return { invalid, system, saved, required, length, text, direct, ordinals,
        foreign, noProvider, wrongName, noMemory, duplicate, wrongType, freshError: second.getLastPInvokeError() };
}

assert.equal(oracle.pinnedResult, 0xf1234568);
const rows = { row: oracle.row, unsupportedRow: oracle.unsupportedRow, pinnedEntryRow: oracle.pinnedEntryRow,
    arrayRow: oracle.arrayRow, stackRow: oracle.stackRow };
const result = await exercise(compiler, assembly, rows, instantiateManagedModule);
assert.deepEqual(result, {
    first: oracle.first, second: oracle.second, nullPointer: oracle.nullPointer, units: oracle.units, staleRejected: true,
    pinnedArray: oracle.pinnedArray, pinnedStack: oracle.pinnedStack, extents: [2, -1, 2],
    retry: oracle.pinnedArray, unconfiguredScopeRejected: true,
    reject: -2, location: [0, oracle.unsupportedRow, oracle.callOffset],
    pinnedReject: -2, pinnedLocation: [0, oracle.pinnedRow, 1],
    imports: [["standardwasm:resources", "memory", "memory"],
        ["standardwasm:native-indirect", "platform:u32(i32,p32)", "function"],
        ["standardwasm:native-indirect", "platform:u32(i32,p32):scoped-utf16", "function"]]
});

const variants = {
    mixed: resolverProbe(), wrongName: resolverProbe({ field: "wrong" }),
    noMemory: resolverProbe({ memory: false }), wrongType: resolverProbe({ wrongType: true }),
    duplicate: resolverProbe({ duplicate: true })
};
const library = await exerciseLibrary(compiler, assembly, framework, oracle.loadLibrary, packEntry, instantiateManagedModule, createTempPathHost);
assert.deepEqual(library.cases, oracle.loadLibrary.cases);
assert.deepEqual(library.symbols, oracle.loadLibrary.symbols);
assert.deepEqual(library.originalSymbol, oracle.loadLibrary.originalSymbol);
assert.deepEqual(library.trySymbols, oracle.loadLibrary.trySymbols);
assert.equal(library.nativeWordAliases, oracle.loadLibrary.nativeWordAliases);
assert.deepEqual(library.unconfigured, [0, 126]);
assert.equal(library.isolatedRegistry, true);
assert.equal(library.required, oracle.loadLibrary.required);
assert.equal(library.length, oracle.loadLibrary.length);
assert.equal(library.text, oracle.loadLibrary.text);
assert.deepEqual(library.originalSuccess, oracle.loadLibrary.original);
assert.deepEqual(library.originalNull, [0, oracle.loadLibrary.cases.at(-1), oracle.loadLibrary.cases.at(-1)]);
assert.ok(library.imports.includes("standardwasm:win32/loadLibraryExW"));
assert.ok(library.imports.includes("standardwasm:native-indirect/" + signature));

const resolver = await exerciseResolver(instantiateManagedModule, createTempPathHost, variants, signature);
assert.deepEqual(resolver, {
    invalid: 0, system: 998, saved: 321, required: 12, length: 11, text: "V:\\Scratch\\\0", direct: 15,
    ordinals: [[0, 0], [1, 1], [1, 1]],
    foreign: true, noProvider: true, wrongName: true, noMemory: true, duplicate: true, wrongType: true, freshError: 0
});

const server = await serveLaunch(0, new Uint8Array());
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = {
        "/compiler": ["application/wasm", compiler],
        "/assembly": ["application/octet-stream", assembly],
        ...Object.fromEntries(framework.map(({ path, bytes }) => ["/" + path, ["application/octet-stream", bytes]]))
    };
    await page.route(server.url + "/**", route => {
        const asset = assets[new URL(route.request().url()).pathname];
        return asset ? route.fulfill({ contentType: asset[0], body: asset[1] }) : route.continue();
    });
    await page.goto(server.url);
    const { edge, edgeResolver, edgeLibrary } = await page.evaluate(async ({
        source, resolverSource, librarySource, rows, variants, signature, loadOracle, frameworkPaths
    }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { packEntry } = await import("/launch/companions.mjs");
        const { createTempPathHost } = await import("/launch/temp-path-host.mjs");
        const [wasm, image] = await Promise.all(["/compiler", "/assembly"]
            .map(async path => (await fetch(path)).arrayBuffer()));
        const framework = await Promise.all(frameworkPaths.map(async path => ({
            path, bytes: new Uint8Array(await (await fetch("/" + path)).arrayBuffer())
        })));
        return {
            edge: await (0, eval)("(" + source + ")")(wasm, image, rows, instantiateManagedModule),
            edgeLibrary: await (0, eval)("(" + librarySource + ")")(wasm, image, framework, loadOracle, packEntry, instantiateManagedModule, createTempPathHost),
            edgeResolver: await (0, eval)("(" + resolverSource + ")")(instantiateManagedModule, createTempPathHost, variants, signature)
        };
    }, { source: exercise.toString(), resolverSource: exerciseResolver.toString(), librarySource: exerciseLibrary.toString(),
        rows, variants, signature, loadOracle: oracle.loadLibrary, frameworkPaths: framework.map(({ path }) => path) });
    assert.deepEqual(edge, result);
    assert.deepEqual(edgeResolver, resolver);
    assert.deepEqual(edgeLibrary, library);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(assembly), clr: oracle.runtime,
        clrArchitecture: oracle.architecture, node: process.version, edge: browser.version(), result, resolver, library };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify(report));
} finally {
    await browser.close();
    await server.close();
}
