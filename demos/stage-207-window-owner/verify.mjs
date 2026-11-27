import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-207-window-owner");
const evidence = join(root, "artifacts/stage-207-window-owner");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0-windows/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
assert.deepEqual({ ...oracle.original, sha256: undefined }, {
    row: 232, library: "USER32.dll", entry: "GetWindowThreadProcessId", SetLastError: false,
    signature: "0002091184BC0F09", sha256: undefined
});
const application = readFileSync(oracle.fixture);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function verify(compiler, fixture, instantiate, makeWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        bytes.forEach((value, index) => tool.SetByte(array, index, value));
        return array;
    }
    const image = upload(new Uint8Array(fixture));
    const encoder = new TextEncoder();
    const manifest = [];
    for (const label of ["Query", "Module", "SeedErrors", "SystemError", "SavedError"]) {
        const name = encoder.encode(label);
        const row = tool.FindRow(image, upload(name));
        if (row < 1) throw Error("Missing fixture method " + label);
        const header = new Uint8Array(8);
        const view = new DataView(header.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, name.length, true);
        manifest.push(...header, ...name);
    }
    const output = tool.CreateBytes(65536), location = tool.CreateDiagnostic();
    const length = tool.CompileModuleDetailed(image, upload(manifest), output, location);
    if (length <= 0)
        throw Error("Fixture status " + length + " at " + [0, 1, 2].map(index => tool.ReadDiagnostic(location, index)));
    const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind]);
    const wrongRow = tool.FindRow(image, upload(encoder.encode("WrongPointee")));
    const wrongLength = tool.CompileDetailed(image, wrongRow, output, location);
    // The Windows oracle never calls the deliberately mismatched declaration.
    const wrongImport = wrongLength > 0 ? WebAssembly.Module.imports(new WebAssembly.Module(
        Uint8Array.from({ length: wrongLength }, (_, i) => tool.ReadByte(output, i)))) : [];
    const exactPointee = wrongRow > 0 && (wrongLength === -2 || wrongLength > 0) &&
        !wrongImport.some(item => item.module === "standardwasm:win32" && item.name === "getWindowThreadProcessId");
    const windows = makeWindows();
    let memory;
    // Module lookup exposes the same resource memory through the existing
    // browser capability setup. The owner query itself forwards only pointers.
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries(registry) { memory = registry.memory; }
    });
    if (guest.Module() !== 1 || !memory) throw Error("Missing executable module or guest memory");
    const address = memory.grow(1) * 65536;
    const view = new DataView(memory.buffer);
    const main = windows.register(0), second = windows.register(0);
    const { threadId, processId } = windows.getWindowOwner(main);

    function errors(exports) {
        return { system: exports.SystemError(), saved: exports.SavedError() };
    }
    function owner(handle) {
        const sentinel = 0x89abcdef, guard = 0x76543210;
        view.setUint32(address, guard, true);
        view.setUint32(address + 4, sentinel, true);
        view.setUint32(address + 8, guard, true);
        guest.SeedErrors(17185, 25106);
        const thread = guest.Query(handle, address + 4) >>> 0;
        const process = view.getUint32(address + 4, true);
        return { thread: thread === threadId, zeroThread: thread === 0, process: process === processId,
            unchangedOutput: process === sentinel, ...errors(guest),
            guards: view.getUint32(address, true) === guard && view.getUint32(address + 8, true) === guard };
    }
    function thread(handle, pointer = 0) {
        guest.SeedErrors(17185, 25106);
        const id = guest.Query(handle, pointer) >>> 0;
        return { thread: id === threadId, zeroThread: id === 0, ...errors(guest) };
    }
    const withOutput = owner(main), noOutput = thread(second), otherWindow = owner(second);
    windows.unregister(main);
    const cases = { withOutput, noOutput, otherWindow, invalid: owner(0), stale: owner(main), invalidNoOutput: thread(0) };
    const invalidPointer = thread(second, -1);
    const invalidHandleBeforePointer = thread(0, -1);
    const afterInvalidPointer = owner(second);
    const endSlot = memory.buffer.byteLength - 4;
    const endResult = thread(second, endSlot);
    const endAddressWorks = endResult.thread && view.getUint32(endSlot, true) === processId;
    const outOfBounds = thread(second, endSlot + 1);
    const endAddressUnchanged = outOfBounds.system === 998 && outOfBounds.zeroThread &&
        view.getUint32(endSlot, true) === processId;

    const freshWindows = makeWindows();
    const { exports: fresh } = await instantiate(module, tool, [], {}, "Fresh.exe", freshWindows);
    fresh.SeedErrors(17185, 25106);
    const foreign = { result: fresh.Query(second, 0), ...errors(fresh) };
    const freshHandle = freshWindows.register(0);
    const freshOwner = freshWindows.getWindowOwner(freshHandle);
    const distinctOwners = freshOwner.threadId !== threadId && freshOwner.processId !== processId;
    const returnedFreshOwner = fresh.Query(freshHandle, 0) >>> 0 === freshOwner.threadId;
    return { imports, cases, safeguards: { exactPointee, invalidPointer, invalidHandleBeforePointer, afterInvalidPointer,
        endAddressWorks, endAddressUnchanged, foreign, distinctOwners, returnedFreshOwner } };
}

const node = await verify(compiler, application, instantiateManagedModule, createWindowState);
assert.deepEqual(node.cases, oracle.results);
assert.deepEqual(node.imports, [
    ["standardwasm:resources", "memory", "memory"],
    ["standardwasm:win32", "getLastSystemError", "function"],
    ["standardwasm:win32", "setLastSystemError", "function"],
    ["standardwasm:win32", "getLastPInvokeError", "function"],
    ["standardwasm:win32", "setLastPInvokeError", "function"],
    ["standardwasm:win32", "getModuleHandleW", "function"],
    ["standardwasm:win32", "getWindowThreadProcessId", "function"]
]);
assert.deepEqual(node.safeguards, {
    exactPointee: true,
    invalidPointer: { thread: false, zeroThread: true, system: 998, saved: 25106 },
    invalidHandleBeforePointer: oracle.results.invalidNoOutput,
    afterInvalidPointer: oracle.results.otherWindow,
    endAddressWorks: true, endAddressUnchanged: true,
    foreign: { result: 0, system: 1400, saved: 25106 },
    distinctOwners: true, returnedFreshOwner: true
});
const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route(server.url + "/fixture", route => route.fulfill({ body: application }));
    await page.goto(server.url);
    const edge = await page.evaluate(async source => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const inputs = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture"].map(async url => (await fetch(url)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, instantiateManagedModule, createWindowState);
    }, verify.toString());
    assert.deepEqual(edge, node);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), fixture: hash(application), oracle, nodeVersion: process.version,
        edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 GetWindowThreadProcessId fixture through the production host.");
} finally {
    await browser?.close();
    await server.close();
}
