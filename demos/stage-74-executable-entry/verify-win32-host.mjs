import { wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { basename, join } from "node:path";
import { chromium } from "playwright-core";
import { instantiateManagedModule } from "./resource-host.mjs";
import { createWindowState } from "./window-state.mjs";

const methods = [
    ["getModuleHandleW", 0], ["getLastSystemError", 1], ["setLastSystemError", 2],
    ["getLastPInvokeError", 1], ["setLastPInvokeError", 2],
    ["createActCtxW", 0], ["getCurrentActCtx", 0], ["activateActCtx", 3],
    ["deactivateActCtx", 3], ["releaseActCtx", 2]
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
        ...section(1, [4, 0x60, 1, 0x7f, 1, 0x7f, 0x60, 0, 1, 0x7f, 0x60, 1, 0x7f, 0, 0x60, 2, 0x7f, 0x7f, 1, 0x7f]),
        ...section(2, [methods.length + 1, ...imports]),
        ...section(7, [methods.length + 1, ...exports])
    ]);
}

async function exercise(instantiate, bytes, executable, framework) {
    const module = await WebAssembly.compile(new Uint8Array(bytes));
    const asset = { path: "framework/" + framework.name, bytes: new Uint8Array(framework.bytes) };
    const make = assets => instantiate(module, {}, assets, {}, "apps/" + executable);
    const { exports: host } = await make([asset]);

    function writeName(address, text, memory = host.memory) {
        const view = new DataView(memory.buffer);
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
    const second = (await make([{ ...asset, loadedFromBytes: true }])).exports;
    const isolated = [second.getLastSystemError(), second.getLastPInvokeError(), second.memory !== host.memory];

    const addr = host.memory.grow(1) * 65536;
    const afterGrowth = host.getModuleHandleW(writeName(addr, "APPS/" + executable.toUpperCase()));
    const otherPath = host.getModuleHandleW(writeName(32, "other/" + executable));
    const pathError = host.getLastSystemError();
    const invalidAddress = host.getModuleHandleW(host.memory.buffer.byteLength - 1);
    const pointerError = host.getLastSystemError();
    host.setLastSystemError(0);
    const winForms = host.getModuleHandleW(writeName(32, framework.name.toUpperCase()));
    const frameworkLookup = [winForms === 0 ? 0 : 1, host.getLastSystemError()];
    const explicitHandle = host.getModuleHandleW(writeName(32, asset.path));
    const embedded = second.getModuleHandleW(writeName(32, framework.name, second.memory));
    const request = addr + 1024;
    const current = request + 32;
    const cookieAddress = current + 4;
    const view = new DataView(host.memory.buffer);
    view.setUint32(request, 32, true);
    view.setUint32(request + 4, 0x88, true);
    view.setUint32(request + 20, 2, true);
    view.setUint32(request + 28, winForms, true);
    host.setLastSystemError(0);
    const context = host.createActCtxW(request);
    const createError = host.getLastSystemError();
    function currentContext() {
        if (!host.getCurrentActCtx(current)) throw Error("Could not query the active context");
        const handle = view.getUint32(current, true);
        if (handle) host.releaseActCtx(handle);
        return handle;
    }
    const before = currentContext();
    const activated = host.activateActCtx(context, cookieAddress) === 1;
    const cookie = view.getUint32(cookieAddress, true);
    const currentMatches = currentContext() === context;
    const contextIsolated = second.getCurrentActCtx(32) === 1 &&
        new DataView(second.memory.buffer).getUint32(32, true) === 0;

    const inner = host.createActCtxW(request);
    host.activateActCtx(inner, cookieAddress);
    const innerCookie = view.getUint32(cookieAddress, true);
    host.releaseActCtx(inner); // The active stack retains its copy.
    const nestedMatches = innerCookie !== cookie && currentContext() === inner &&
        host.deactivateActCtx(0, innerCookie) === 1 && currentContext() === context;
    const restored = host.deactivateActCtx(0, cookie) === 1 && currentContext() === before;
    host.releaseActCtx(context);

    view.setUint32(request + 20, 65535, true);
    const missing = [host.createActCtxW(request), host.getLastSystemError()];
    view.setUint32(request + 20, 2, true);
    view.setUint32(request + 4, 0x80000088, true);
    const invalidFlags = [host.createActCtxW(request), host.getLastSystemError()];
    const invalidRequest = [host.createActCtxW(host.memory.buffer.byteLength - 31), host.getLastSystemError()];
    const activationContext = { createError, activated, currentMatches, restored, missing, invalidFlags,
        contextIsolated, nestedMatches, invalidRequest, validHandle: context > 0 };

    return { results, errors, isolated, activationContext, pathMatches: afterGrowth === processHandle,
        otherPath, pathError, invalidAddress, pointerError, nonzeroProcessHandle: processHandle !== 0, frameworkLookup,
        stableFrameworkHandle: winForms !== 0 && winForms === explicitHandle && winForms !== processHandle,
        embeddedFramework: [embedded, second.getLastSystemError()] };
}

async function exerciseCompiler(instantiate, makeWindows, compilerBytes, fixtureBytes, framework, dpiScenarios, runtimeAssets) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(new Uint8Array(compilerBytes));
    function upload(bytes) {
        const result = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(result, i, bytes[i]);
        return result;
    }

    const fixture = upload(fixtureBytes);
    async function compile(exportNames, companions = null) {
        const encoder = new TextEncoder();
        const names = exportNames.map(name => encoder.encode(name));
        const manifest = new Uint8Array(names.reduce((size, name) => size + 8 + name.length, 0));
        const view = new DataView(manifest.buffer);
        let offset = 0;
        for (const name of names) {
            const row = compiler.FindRow(fixture, upload(name));
            if (row <= 0) throw Error("Missing Win32 fixture export " + new TextDecoder().decode(name));
            view.setInt32(offset, row, true);
            view.setInt32(offset + 4, name.length, true);
            manifest.set(name, offset + 8);
            offset += 8 + name.length;
        }

        const output = compiler.CreateBytes(companions?.length ? 1 << 20 : 65536);
        const location = compiler.CreateDiagnostic();
        let length;
        if (companions !== null) {
            const images = [fixtureBytes, ...companions];
            const bundle = new Uint8Array(images.reduce((size, image) => size + 4 + image.length, 0));
            const lengths = new DataView(bundle.buffer);
            let cursor = 0;
            for (const image of images) {
                lengths.setUint32(cursor, image.length, true);
                bundle.set(image, cursor + 4);
                cursor += 4 + image.length;
            }
            length = compiler.CompileBundleDetailed(upload(bundle), upload(manifest), output, location);
        } else {
            length = compiler.CompileModuleDetailed(fixture, upload(manifest), output, location);
        }
        if (length <= 0) throw Error("Win32 fixture failed to compile at " +
            [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index)));
        const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
        return WebAssembly.compile(code);
    }

    const module = await compile(["Exchange", "Read", "LookupModule", "Create", "Lifecycle", "PointerRoundTrip", "NativeLocalAlias", "NativeAggregateAlias",
        "Alias", "Sum", "LockRoundTrip", "ReleaseDepth", "MatchPair", "ObjectSpanAliases", "ObjectSpanBounds",
        "DpiValid", "DpiAwareness", "DpiApply", "DpiCurrent", "DpiIsAware", "BooleanAliases", "BooleanWithMonitor",
        "GenericBooleanLocations", "GenericStringLocations"]);
    const nativeImports = new Set(WebAssembly.Module.imports(module)
        .filter(entry => entry.module === "standardwasm:win32").map(entry => entry.name));
    for (const name of ["createActCtxW", "getCurrentActCtx", "activateActCtx", "deactivateActCtx", "releaseActCtx"])
        if (!nativeImports.has(name)) throw Error("The generated fixture omitted " + name);
    const { exports } = await instantiate(module, compiler,
        [{ path: framework.name, bytes: new Uint8Array(framework.bytes) }]);
    const legacy = [exports.Exchange(19), exports.Exchange(-13), exports.Read(),
        exports.LookupModule(0), exports.LookupModule(1), exports.LookupModule(2), exports.LookupModule(3)];
    const activation = [0, 1, 2, 3].map(scenario => {
        const handle = exports.Create(scenario);
        return [handle === -1 ? -1 : handle > 0 ? 1 : 0, exports.Read()];
    });
    const pointers = [0, -1, 123].flatMap(value => [exports.PointerRoundTrip(value, 0), exports.PointerRoundTrip(value, 1)]);
    const enumModule = await compile(["EnumVectors"], []);
    const { exports: enumExports } = await instantiate(enumModule, compiler, []);
    const enumVectors = [0, 1, 2, 3, 4].map(enumExports.EnumVectors);
    const nativeLocal = [0, -1, 123].map(value => exports.NativeLocalAlias(value));
    const nativeAggregate = [0, -1, 123].map(value => exports.NativeAggregateAlias(value));

    const windowModule = await compile(["ReadWindowEnabled", "WriteWindowEnabled"]);
    const windowImports = WebAssembly.Module.imports(windowModule);
    if (windowImports.length !== 2 || windowImports.some(entry => entry.module !== "standardwasm:win32") ||
        !windowImports.some(entry => entry.name === "isWindowEnabled") || !windowImports.some(entry => entry.name === "enableWindow"))
        throw Error("The window fixture must use only the expected Win32 imports.");
    const windows = makeWindows();
    let handle;
    let query;
    let messages;
    handle = windows.register(0, (message, wParam) => messages.push([message, wParam, query(handle)]));
    const disabled = windows.register(0x08000000);
    const { exports: window } = await instantiate(windowModule, compiler, [], {}, undefined, windows);
    query = window.ReadWindowEnabled;
    const queries = [query(0), query(handle), query(disabled)];
    const { exports: otherWindow } = await instantiate(windowModule, compiler, []);
    if (otherWindow.ReadWindowEnabled(handle) !== 0) throw Error("Window handles leaked across instances.");
    const transitions = [1, 0, 0, 1, 1].map(request => {
        messages = [];
        const previous = window.WriteWindowEnabled(handle, request);
        return { previous, enabled: query(handle), messages };
    });
    if (!windows.unregister(handle)) throw Error("The window was not registered.");
    queries.push(query(handle));
    const windowEnabled = { queries, transitions, invalid: window.WriteWindowEnabled(handle, 1) };

    const randomModule = await compile(["CheckSystemRandomBuffer", "CheckSystemRandomStatus", "CheckSystemRandomRange"], runtimeAssets);
    const randomImports = WebAssembly.Module.imports(randomModule);
    if (randomImports.length !== 2 ||
        !randomImports.some(entry => entry.module === "standardwasm:resources" && entry.name === "memory") ||
        !randomImports.some(entry => entry.module === "standardwasm:win32" && entry.name === "bCryptGenRandom"))
        throw Error("The random fixture must use only the random source and its linear memory.");
    const { exports: random } = await instantiate(randomModule, compiler, []);
    const systemRandom = [0, 16, 65536, 65552, 131104].map(random.CheckSystemRandomBuffer);
    const randomStatuses = [0, 1, 2, 3].map(random.CheckSystemRandomStatus);
    const invalidRandomArgument = 0xc000000d | 0;
    if (random.CheckSystemRandomRange(-1, 16) !== invalidRandomArgument ||
        random.CheckSystemRandomRange(1024, -1) !== invalidRandomArgument)
        throw Error("The random host accepted a range outside Wasm memory.");

    const staticPin = [exports.Sum(), exports.Alias(2), exports.Sum(), exports.Alias(9), exports.Sum()];
    const monitor = [0, 1, 2, 3].map(exports.LockRoundTrip).concat([0, 1, 2].map(exports.ReleaseDepth));
    const filter = [0, 1, 2, 3].map(exports.MatchPair);
    const objectSpans = [0, 1].map(exports.ObjectSpanAliases).concat([-1, 0, 1].map(exports.ObjectSpanBounds));
    const booleanLocations = [0, 1, 2].map(exports.BooleanAliases)
        .concat([0, 1].map(exports.BooleanWithMonitor));
    const genericLocations = [0, 1, 2].map(exports.GenericBooleanLocations)
        .concat([0, 1, 2].map(exports.GenericStringLocations));
    const bundledModule = await compile(["InvokeFinalWriter", "ReadGenericValue",
        "InvokeConstrainedReceiver", "CheckWidenedIdentity", "CheckGenericDescriptors", "CheckValueAlias",
        "VerifyGenericValueMethod", "VerifyGenericDefaultReference", "ReadStaticByteSpan", "ReadStaticByteLocation"], []);
    const { exports: bundledExports } = await instantiate(bundledModule, compiler, []);
    const finalWriter = [0, 1].map(bundledExports.InvokeFinalWriter);
    const genericValues = [-1, 0, 1].map(bundledExports.ReadGenericValue);
    const constrainedReferences = [0, 1, 2].map(bundledExports.InvokeConstrainedReceiver)
        .concat([0, 1, 2].map(bundledExports.CheckWidenedIdentity));
    const genericDescriptors = [0, 1, 2].map(bundledExports.CheckGenericDescriptors);
    const valueAliases = [0, 1, 2].map(bundledExports.CheckValueAlias);
    const genericValueMethods = [0, 1, 2].map(bundledExports.VerifyGenericValueMethod)
        .concat([0, 1, 2].map(bundledExports.VerifyGenericDefaultReference));
    const byteSpan = [-1, 0, 1, 2, 3, 4].map(bundledExports.ReadStaticByteSpan);
    const byteLocations = [0, 1, 2].map(bundledExports.ReadStaticByteLocation);
    const rootModule = await compile(["CompareThroughRoot", "CompareReturnedType", "CheckCoreStringFirstCharacter",
        "CheckCoreStringContains", "CheckCoreWhitespace", "CheckCoreStringTrim", "ScopedWritableSpan", "CheckStackBackedSpan", "CheckScopedReference",
        "CheckScopedReceiverReset", "CheckSpanFromEnd", "CheckCharacterCallback", "CheckStringCreation",
        "CheckStaticEventCallback", "ExactValueRead"], runtimeAssets);
    const { exports: rootExports } = await instantiate(rootModule, compiler, []);
    const rootDispatch = [0, 1, 2, 3].map(rootExports.CompareThroughRoot);
    const typeReturn = [0, 1].map(rootExports.CompareReturnedType);
    const stringField = [0, 1, 2, 3, 4].map(rootExports.CheckCoreStringFirstCharacter);
    const stringContains = [0, 1, 2, 3, 4, 5, 6, 7, 8].map(rootExports.CheckCoreStringContains);
    const stringTrim = [0, 1, 2, 3, 4, 5, 6, 7].map(rootExports.CheckCoreStringTrim);
    const scopedWritableSpan = [0, 65, 0x2000, 0xffff].map(rootExports.ScopedWritableSpan);
    const scopedReceiverReset = [0, 65, 0x2000, 0xffff].map(rootExports.CheckScopedReceiverReset);
    const spanFromEnd = [0, 1, 2, 3].map(rootExports.CheckSpanFromEnd);
    const stackBackedSpan = [0, 1, 2, 3, 4, 5].map(rootExports.CheckStackBackedSpan);
    const scopedReference = [0, -1, 65, 0xffff].map(rootExports.CheckScopedReference);
    const characterCallbacks = [-1, 0, 1, 2, 17].map(rootExports.CheckCharacterCallback);
    const stringCreation = [0, 1, 2, 3, 4].map(rootExports.CheckStringCreation);
    const staticEventCallbacks = [0, 1, 2].map(rootExports.CheckStaticEventCallback);
    const exactValueReads = [-2147483648, -5, 0, 5, 2147483647].map(rootExports.ExactValueRead);
    // Keeping a monitor import in the same module verifies the runtime import
    // index when both groups are present.
    const debuggerModule = await compile(["ReadManagedDebuggerStatus", "LockRoundTrip"], runtimeAssets);
    const debuggerImports = WebAssembly.Module.imports(debuggerModule);
    if (!debuggerImports.some(entry => entry.module === "standardwasm:runtime" && entry.name === "isDebuggerAttached") ||
        !debuggerImports.some(entry => entry.module === "standardwasm:threading" && entry.name === "changeMonitorDepth"))
        throw Error("The debugger fixture must use both host imports.");
    const { exports: debuggerDefault } = await instantiate(debuggerModule, compiler, []);
    const debuggerHost = { attached: false, isDebuggerAttached() { return this.attached; } };
    const { exports: debuggerCustom } = await instantiate(debuggerModule, compiler, [], {
        "standardwasm:runtime": debuggerHost
    });
    const debuggerStatus = { default: debuggerDefault.ReadManagedDebuggerStatus(), transitions: [false, true, false].map(value => {
        debuggerHost.attached = value;
        return debuggerCustom.ReadManagedDebuggerStatus();
    }) };
    const whitespace = [0, 32, 133, 160, 5760, 8192, 8203, 8232, 12288, 65535].map(rootExports.CheckCoreWhitespace);
    const dpi = [];
    // Each Windows oracle invocation starts a fresh process.
    for (const scenario of dpiScenarios) {
        const { exports: probe } = await instantiate(module, compiler, []);
        const contexts = [0, -1, -2, -3, -4, -5, -6];
        dpi.push({
            valid: contexts.map(probe.DpiValid), awareness: contexts.map(probe.DpiAwareness),
            initial: probe.DpiCurrent(),
            steps: scenario.map(mode => [probe.DpiApply(mode), probe.Read(), probe.DpiCurrent(), probe.DpiIsAware()])
        });
    }
    return { legacy, activation, lifecycle: exports.Lifecycle(), pointers, nativeLocal, nativeAggregate, enumVectors, windowEnabled, systemRandom, randomStatuses, staticPin, monitor, filter, objectSpans, booleanLocations, genericLocations, finalWriter, genericValues, constrainedReferences, genericDescriptors, valueAliases, genericValueMethods, rootDispatch, typeReturn, stringField, stringContains, stringTrim, scopedWritableSpan, scopedReceiverReset, spanFromEnd, stackBackedSpan, scopedReference, characterCallbacks, stringCreation, staticEventCallbacks, debuggerStatus, exactValueReads, byteSpan, byteLocations, whitespace, dpi };
}

function readOracle(...args) {
    return JSON.parse(execFileSync("dotnet", ["run", "--file",
        fileURLToPath(new URL("./Win32HostOracle.cs", import.meta.url)), "--", ...args], { encoding: "utf8" }));
}

const oracle = readOracle();
const dpiScenarios = [[0, -4, -2, 98], [98, -4], [99, 102, 101, 103], [-5, -4]];
const compilerPath = process.argv[2];
const framework = { name: basename(oracle.framework), bytes: readFileSync(oracle.framework) };
const runtime = compilerPath ? wasm32Runtime() : null;
const runtimeAssets = runtime ? ["System.Private.CoreLib.dll", "System.Runtime.dll"]
    .map(name => readFileSync(join(runtime.directory, name))) : [];
const runtimePaths = runtimeAssets.map((_, index) => "/test-runtime-" + index);
const expectedCompiler = {
    legacy: oracle.compiledFixture, activation: oracle.compiledActivation, lifecycle: oracle.compiledLifecycle,
    pointers: oracle.compiledPointers, nativeLocal: oracle.nativeLocal, nativeAggregate: oracle.nativeAggregate, enumVectors: oracle.enumVectors, windowEnabled: oracle.windowEnabled, staticPin: oracle.compiledStaticPin, monitor: oracle.compiledMonitor,
    filter: oracle.compiledFilter, objectSpans: oracle.compiledObjectSpans, booleanLocations: oracle.compiledBooleanLocations,
    genericLocations: oracle.compiledGenericLocations, finalWriter: oracle.compiledFinalWriter,
    genericValues: oracle.compiledGenericValues, constrainedReferences: oracle.compiledConstrainedReferences,
    genericDescriptors: oracle.compiledGenericDescriptors, valueAliases: oracle.compiledValueAliases, rootDispatch: oracle.compiledRootDispatch,
    typeReturn: oracle.compiledTypeReturn, stringField: oracle.compiledStringField, byteSpan: oracle.compiledByteSpan,
    stringContains: oracle.compiledStringContains, stringTrim: oracle.compiledStringTrim,
    scopedWritableSpan: oracle.scopedWritableSpan, stackBackedSpan: oracle.stackBackedSpan, scopedReference: oracle.scopedReference,
    scopedReceiverReset: oracle.scopedReceiverReset, spanFromEnd: oracle.spanFromEnd,
    systemRandom: oracle.systemRandom, randomStatuses: oracle.randomStatuses,
    characterCallbacks: oracle.characterCallbacks, staticEventCallbacks: oracle.staticEventCallbacks,
    stringCreation: oracle.stringCreation,
    debuggerStatus: { default: oracle.debuggerStatus, transitions: [0, 1, 0] },
    exactValueReads: oracle.exactValueReads,
    byteLocations: oracle.compiledByteLocations,
    whitespace: oracle.compiledWhitespace,
    genericValueMethods: oracle.compiledGenericValueMethods
};
let compilerBytes, fixtureBytes;
if (compilerPath) {
    execFileSync("dotnet", ["build", fileURLToPath(new URL("./Win32Fixture", import.meta.url)), "-c", "Release"],
        { stdio: "inherit" });
    compilerBytes = readFileSync(compilerPath);
    fixtureBytes = readFileSync(new URL("./Win32Fixture/bin/Release/net10.0/Win32Fixture.dll", import.meta.url));
    expectedCompiler.dpi = dpiScenarios.map(scenario => readOracle("dpi", ...scenario.map(String)));
    assert.deepEqual(await exerciseCompiler(instantiateManagedModule, createWindowState, compilerBytes, fixtureBytes, framework, dpiScenarios, runtimeAssets), expectedCompiler);
}
const bytes = probeModule();
const node = await exercise(instantiateManagedModule, bytes, oracle.executable, framework);
const { createError, activated, currentMatches, restored, missing, invalidFlags } = oracle.activationContext;
assert.deepEqual(node, { activationContext: {
    createError, activated, currentMatches, restored, missing: [-1, missing], invalidFlags: [-1, invalidFlags],
    contextIsolated: true, nestedMatches: true, invalidRequest: [-1, 998], validHandle: true
}, results: oracle.results, errors: oracle.errors,
    isolated: [0, 0, true], pathMatches: true, otherPath: 0, pathError: 126,
    invalidAddress: 0, pointerError: 998, nonzeroProcessHandle: true, frameworkLookup: oracle.frameworkLookup,
    stableFrameworkHandle: true, embeddedFramework: [0,126] });

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const base = "http://127.0.0.1";
    await page.route(base + "/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        const runtimeIndex = runtimePaths.indexOf(path);
        if (runtimeIndex >= 0 && runtimeAssets[runtimeIndex])
            return route.fulfill({ contentType: "application/octet-stream", body: runtimeAssets[runtimeIndex] });
        if (path === "/test-framework")
            return route.fulfill({ contentType: "application/octet-stream", body: framework.bytes });
        if (path === "/stage-73-executable-intake/loader/pe.mjs")
            return route.fulfill({ contentType: "text/javascript",
                body: readFileSync(new URL("../stage-73-executable-intake/loader/pe.mjs", import.meta.url)) });
        if (["/resource-host.mjs", "/win32-host.mjs", "/activation-context.mjs", "/dpi-awareness.mjs", "/window-state.mjs"].includes(path))
            return route.fulfill({ contentType: "text/javascript", body: readFileSync(new URL("." + path, import.meta.url)) });
        return route.abort();
    });
    await page.goto(base);
    const edge = await page.evaluate(async ({ source, bytes, executable, frameworkName }) => {
        const { instantiateManagedModule } = await import("/resource-host.mjs");
        const frameworkBytes = await (await fetch("/test-framework")).arrayBuffer();
        return (0, eval)("(" + source + ")")(instantiateManagedModule, bytes, executable,
            { name: frameworkName, bytes: frameworkBytes });
    }, { source: exercise.toString(), bytes: [...bytes], executable: oracle.executable, frameworkName: framework.name });
    assert.deepEqual(edge, node);
    if (compilerBytes) {
        const compiled = await page.evaluate(async ({ source, compilerBytes, fixtureBytes, name, dpiScenarios, runtimePaths }) => {
            const { instantiateManagedModule } = await import("/resource-host.mjs");
            const { createWindowState } = await import("/window-state.mjs");
            const bytes = await (await fetch("/test-framework")).arrayBuffer();
            const runtimeAssets = await Promise.all(runtimePaths.map(async path =>
                new Uint8Array(await (await fetch(path)).arrayBuffer())));
            return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, compilerBytes, fixtureBytes, { name, bytes },
                dpiScenarios, runtimeAssets);
        }, { source: exerciseCompiler.toString(), compilerBytes: [...compilerBytes], fixtureBytes: [...fixtureBytes],
            name: framework.name, dpiScenarios, runtimePaths });
        assert.deepEqual(compiled, expectedCompiler);
        console.log("Managed debugger status and host transitions passed in Node and Edge.");
        console.log("Five closed generic value reads matched the CLR in Node and Edge.");
        console.log("Five enum vector cases matched the CLR in Node and Edge, including typed failures.");
        console.log("Six generic value method and reference default cases matched the CLR in Node and Edge.");
        console.log("Six readonly static byte span reads and bounds cases matched the CLR in Node and Edge.");
        console.log("Static byte references, unaligned reads, and CoreLib whitespace classification matched the CLR in Node and Edge.");
        console.log("Eight original CoreLib trim cases matched CLR UTF-16 content and reference identity in Node and Edge.");
        console.log("Four scoped span cases matched CLR aliasing, default length, and UTF-16 wraparound in Node and Edge.");
        console.log("Six stack-backed span results matched the CLR in Node and Edge, including typed bounds and length failures.");
        console.log("Four scoped byref cases matched CLR field mutation, span replacement, and value-copy independence in Node and Edge.");
        console.log("Four scoped receiver resets matched CLR alias updates and preserved earlier copies in Node and Edge.");
        console.log("Five system-random buffer sizes and four return statuses matched Windows in Node and Edge.");
        console.log("Five span callbacks and three static event cases matched the CLR in Node and Edge.");
        console.log("Five original CoreLib String.StartsWith(char) cases matched the CLR in Node and Edge.");
        console.log("Nine original CoreLib String.Contains(char) cases matched the CLR in Node and Edge.");
        console.log("Both returned runtime type cases matched the CLR in Node and Edge.");
        console.log("Four original CoreLib virtual dispatch cases matched the CLR in Node and Edge.");
        console.log("Three struct alias, copy, and reset cases matched the CLR in Node and Edge.");
        console.log("Three closed generic type identity cases matched the CLR in Node and Edge.");
        console.log("Six constrained reference dispatch and identity cases matched the CLR in Node and Edge.");
        console.log("Three generic value construction and default cases matched the CLR in Node and Edge.");
        console.log("Calls to a final generic method matched the CLR for live and null receivers.");
        console.log("Six generic Boolean and string location cases matched the CLR in Node and Edge.");
        console.log("Five managed Boolean alias cases matched the CLR in Node and Edge.");
        console.log("Four isolated DPI startup sequences matched Windows CLR in Node and Edge.");
        console.log("Single-object spans matched CLR argument and local aliasing and typed bounds failures.");
        console.log("Two-type exception filters matched the CLR for both matches, a rejection, and normal completion.");
        console.log("Monitor reentrancy, lockTaken, and three typed failure paths matched the CLR.");
        console.log("Pinned static aliases matched the CLR across managed stores and separate export calls.");
        console.log("Native word and blittable local aliases matched the CLR in Node and Edge.");
        console.log("Window queries, five enable transitions, and synchronous message order matched Windows in Node and Edge.");
        console.log("The Wasm compiler matched CLR module lookups, the activation-context lifecycle, and Marshal error exchange in Node and Edge.");
    }
    console.log("Node and Edge matched 6 CLR module lookups and both last-error slots. Memory growth and host isolation passed.");
    console.log("Node and Edge matched the WinForms manifest activation lifecycle and Windows error results.");
    console.log("Nested activation, browser instance isolation, and Wasm32 request bounds passed.");
} finally {
    await browser.close();
}
