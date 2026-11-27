import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { windowsScalars } from "../../scripts/native-imports.mjs";
import { createDynamicLibraryHost } from "./dynamic-library-host.mjs";
import { createTempPathHost } from "./temp-path-host.mjs";

const artifactRoot = join(root, "artifacts", "temp-path-host");
mkdirSync(artifactRoot, { recursive: true });
const evidence = mkdtempSync(join(artifactRoot, "run-"));
const oraclePath = join(evidence, "oracle.json");
const project = join(root, "demos", "stage-74-executable-entry", "TempPathOracle");
runOn32BitClr(wasm32Runtime(), project, join(project, "bin", "Release", "net10.0", "TempPathOracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");

function exercise(create, path, capacities) {
    const memory = new WebAssembly.Memory({ initial: 1 });
    const errors = { system: 777, pinvoke: 321 };
    const { getTempPathW, getTempPath2W } = create(memory, path, errors);
    const offset = 31; // UTF-16 writes need not be naturally aligned in Wasm.
    memory.grow(1);
    const observe = query => capacities.map(capacity => {
        const buffer = new DataView(memory.buffer, offset, 2 * (capacity + 2));
        for (let i = 0; i < capacity + 2; i++) buffer.setUint16(2 * i, 33, true);
        errors.system = 777;
        const count = query(capacity, capacity ? offset + 2 : 0);
        const text = Array.from({ length: capacity + 2 }, (_, i) => String.fromCharCode(buffer.getUint16(2 * i, true))).join("");
        return { capacity, count, system: errors.system, pinvoke: errors.pinvoke, text };
    });

    const legacy = observe(getTempPathW);
    const current = observe(getTempPath2W);
    const end = new Uint8Array(memory.buffer);
    end[end.length - 1] = 97;
    const invalid = [getTempPathW(1, 0), getTempPath2W(1, end.length - 1), getTempPathW(-1, 31), errors.system, errors.pinvoke, end[end.length - 1]];
    const secondMemory = new WebAssembly.Memory({ initial: 1 });
    const secondErrors = { system: 0, pinvoke: 987 };
    const { getTempPathW: other } = create(secondMemory, "V:\\Scratch\\", secondErrors);
    const secondPathLength = other(20, 8);
    const secondWords = new Uint16Array(secondMemory.buffer, 8, secondPathLength + 1);
    const secondPath = String.fromCharCode(...secondWords);
    const isolated = [secondPath, secondErrors.system, secondErrors.pinvoke, errors.system, errors.pinvoke];
    const rejectedDirectories = ["relative\\", "C:\\missing-slash", "C:\\other\\..\\", "C:\\\0\\"].map(directory => {
        try { create(memory, directory, errors); }
        catch (error) { return error instanceof TypeError; }
        return false;
    });
    return { legacy, current, invalid, isolated, rejectedDirectories };
}

function exerciseRegistry(createRegistry, createTempPath, scalars, oracle) {
    const tempSignature = "platform:u32(i32,p32)";
    const mulDivSignature = "stdcall:i32(i32,i32,i32)";
    const errors = { system: 0, pinvoke: 321 };
    const memory = new WebAssembly.Memory({ initial: 1 });
    const registry = createRegistry(errors);
    const absent = registry.loadLibrary("StandardWasm-Missing-Fixture.dll", 0, 0x800);
    const missingModuleError = errors.system;
    const temp = createTempPath(memory, oracle.path, errors);
    registry.registerLibrary("kernel32", new Map([
        ["GetTempPathW", { signature: tempSignature, invoke: temp.getTempPathW }],
        ["GetTempPath2W", { signature: tempSignature, invoke: temp.getTempPath2W }],
        ["MulDiv", { signature: mulDivSignature, invoke: (...args) => scalars["kernel32!MulDiv"](errors, ...args) }]
    ]));
    registry.registerLibrary("test.bin", new Map([
        ["Bits", { signature: "cdecl:u64(u64)", invoke: bits => bits }],
        ["WrongResult", { signature: "cdecl:i32()", invoke: () => 1n }]
    ]));

    const beforeLoad = registry.getModuleHandle("kernel32");
    const library = registry.loadLibrary("KERNEL32.DLL", 0, 0x800);
    const sameHandle = registry.loadLibrary("kernel32") === library && registry.getModuleHandle("Kernel32") === library;
    const entry = registry.getProcAddress(library, "GetTempPathW");
    const misspelled = registry.getProcAddress(library, "gettemppathw");
    const missingExportError = errors.system;
    const current = registry.getProcAddress(library, "GetTempPath2W");
    const additional = registry.getProcAddress(library, "MulDiv");

    // The fixture has the same three i32 operands as the eventual Wasm
    // dispatch wrapper. The empty import name belongs only to this fixture.
    const abi = new WebAssembly.Module(Uint8Array.of(
        0, 97, 115, 109, 1, 0, 0, 0,
        1, 8, 1, 0x60, 3, 0x7f, 0x7f, 0x7f, 1, 0x7f,
        2, 5, 1, 0, 0, 0, 0,
        7, 9, 1, 5, 0x71, 0x75, 0x65, 0x72, 0x79, 0, 0
    ));
    const { query } = new WebAssembly.Instance(abi, { "": {
        "": (token, count, pointer) => registry.invoke(token, tempSignature, count, pointer)
    } }).exports;
    const runTemp = token => {
        const required = query(token, 0, 0);
        const length = query(token, required, 8);
        return [required, length, String.fromCharCode(...new Uint16Array(memory.buffer, 8, required))];
    };

    const pathCalls = [runTemp(entry), runTemp(current)];
    const oracleShape = {
        missingModule: absent === 0,
        missingModuleError,
        addressResolved: entry !== 0,
        caseSensitive: misspelled === 0,
        missingExportError,
        distinctExports: entry !== additional,
        additionalResult: registry.invoke(additional, mulDivSignature, 10, 3, 2)
    };

    const denied = (operation, pattern) => {
        try { operation(); }
        catch (error) { return pattern.test(error.message); }
        return false;
    };
    const secondErrors = { system: 0, pinvoke: 987 };
    const second = createRegistry(secondErrors);
    const isolated = denied(() => second.invoke(entry, tempSignature, 0, 0), /unregistered/) &&
        second.getProcAddress(library, "GetTempPathW") === 0;
    const secondMemory = new WebAssembly.Memory({ initial: 1 });
    const configuredPath = "V:\\Scratch\\";
    const legacyTemp = createTempPath(secondMemory, configuredPath, secondErrors);
    second.registerLibrary("kernel32.dll", new Map([
        ["GetTempPathW", { signature: tempSignature, invoke: legacyTemp.getTempPathW }]
    ]));
    const legacyLibrary = second.loadLibrary("kernel32", 0, 0x800);
    const possibleCurrent = second.getProcAddress(legacyLibrary, "GetTempPath2W");
    const missingCurrentError = secondErrors.system;
    const selected = possibleCurrent || second.getProcAddress(legacyLibrary, "GetTempPathW");
    secondErrors.system = 0; // CoreLib clears this slot immediately before calling the export.
    const fallbackLength = second.invoke(selected, tempSignature, 20, 8);
    const fallbackPath = String.fromCharCode(...new Uint16Array(secondMemory.buffer, 8, fallbackLength + 1));
    const fallback = [possibleCurrent, missingCurrentError, fallbackPath, secondErrors.system, secondErrors.pinvoke];
    const badHandle = registry.getProcAddress(1, "GetTempPathW");
    const badHandleError = errors.system;
    const diskPath = registry.loadLibrary("C:\\Windows\\System32\\kernel32.dll", 0, 0x800);
    const diskPathError = errors.system;
    const badFlags = registry.loadLibrary("kernel32", 0, 2);
    const badFlagsError = errors.system;
    const scalarLibrary = registry.loadLibrary("test.bin");
    const scalarEntry = registry.getProcAddress(scalarLibrary, "Bits");
    const badResult = registry.getProcAddress(scalarLibrary, "WrongResult");
    const invalidCalls = [
        denied(() => registry.invoke(entry, mulDivSignature, 0, 0), /signature mismatch/),
        denied(() => registry.invoke(entry, tempSignature, 0), /signature mismatch/),
        denied(() => registry.invoke(entry, tempSignature, 0, 1n), /invalid dynamic export argument/),
        denied(() => registry.invoke(badResult, "cdecl:i32()"), /invalid dynamic export result/),
        denied(() => registry.registerLibrary("other", new Map([
            ["bad", { signature: "platform:void(void)", invoke() {} }]
        ])), /invalid dynamic export/)
    ];
    const bits = registry.invoke(scalarEntry, "cdecl:u64(u64)", -1n).toString();

    const sameToken = registry.getProcAddress(library, "GetTempPathW") === entry;
    const firstFree = registry.freeLibrary(library);
    const remaining = query(entry, 0, 0);
    const lastFree = registry.freeLibrary(library);
    const revoked = denied(() => query(entry, 0, 0), /unregistered/);
    const deadHandle = registry.getProcAddress(library, "GetTempPathW");
    const deadHandleError = errors.system;
    const reopened = registry.loadLibrary("kernel32", 0, 0x800);
    const fresh = reopened !== library && registry.getProcAddress(reopened, "GetTempPathW") !== entry;
    return {
        oracleShape, pathCalls, isolated, fallback, invalidCalls, bits,
        handles: [beforeLoad, sameHandle, sameToken, firstFree, remaining, lastFree, revoked, deadHandle, deadHandleError, fresh],
        failures: [badHandle, badHandleError, diskPath, diskPathError, badFlags, badFlagsError],
        pinvokeError: errors.pinvoke
    };
}

const capacities = oracle.current.map(({ capacity }) => capacity);
const node = exercise(createTempPathHost, oracle.path, capacities);
const nodeRegistry = exerciseRegistry(createDynamicLibraryHost, createTempPathHost, windowsScalars, oracle);
assert.deepEqual(nodeRegistry.oracleShape, oracle.registry);
const required = oracle.path.length + 1;
assert.deepEqual(nodeRegistry.pathCalls, Array(2).fill([required, required - 1, oracle.path + "\0"]));
assert.equal(nodeRegistry.isolated, true);
assert.deepEqual(nodeRegistry.fallback, [0, 127, "V:\\Scratch\\\0", 0, 987]);
assert.deepEqual(nodeRegistry.invalidCalls, [true, true, true, true, true]);
assert.equal(nodeRegistry.bits, "18446744073709551615");
assert.deepEqual(nodeRegistry.handles, [0, true, true, 1, required, 1, true, 0, 6, true]);
assert.deepEqual(nodeRegistry.failures, [0, 6, 0, 126, 0, 87]);
assert.equal(nodeRegistry.pinvokeError, 321);
assert.deepEqual(node.legacy, oracle.legacy);
assert.deepEqual(node.current, oracle.current);
assert.deepEqual(node.invalid, [0, 0, 0, 998, 321, 97]);
assert.deepEqual(node.isolated, ["V:\\Scratch\\\0", 0, 987, 998, 321]);
assert.deepEqual(node.rejectedDirectories, [true, true, true, true]);

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const modules = new Map([
        ["/temp-path-host.mjs", new URL("./temp-path-host.mjs", import.meta.url)],
        ["/dynamic-library-host.mjs", new URL("./dynamic-library-host.mjs", import.meta.url)],
        ["/native-imports.mjs", new URL("../../scripts/native-imports.mjs", import.meta.url)]
    ]);
    await page.route("http://127.0.0.1/**", route => {
        const url = modules.get(new URL(route.request().url()).pathname);
        if (url) return route.fulfill({ contentType: "text/javascript", body: readFileSync(url) });
        return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
    });
    await page.goto("http://127.0.0.1/");
    const { edge, edgeRegistry } = await page.evaluate(async ({ source, registrySource, oracle, capacities }) => {
        const { createTempPathHost } = await import("/temp-path-host.mjs");
        const { createDynamicLibraryHost } = await import("/dynamic-library-host.mjs");
        const { windowsScalars } = await import("/native-imports.mjs");
        return {
            edge: (0, eval)("(" + source + ")")(createTempPathHost, oracle.path, capacities),
            edgeRegistry: (0, eval)("(" + registrySource + ")")(createDynamicLibraryHost, createTempPathHost, windowsScalars, oracle)
        };
    }, { source: exercise.toString(), registrySource: exerciseRegistry.toString(), oracle, capacities });
    assert.deepEqual(edge, node);
    assert.deepEqual(edgeRegistry, nodeRegistry);
    const results = { oracle, node, edge, nodeRegistry, edgeRegistry, nodeVersion: process.version, edgeVersion: browser.version() };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2) + "\n");
    console.log("Node and Edge matched " + capacities.length + " buffer cases for both Windows exports, including the system error slot and Unicode output.");
    console.log("Dynamic loading matched Windows lookup results and exercised typed dispatch, missing exports, and per-instance lifetimes.");
} finally {
    await browser.close();
}
