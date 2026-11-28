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

const stage = join(root, "demos/stage-209-thread-handle-lifecycle");
const evidence = join(root, "artifacts/stage-209-thread-handle-lifecycle");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const application = readFileSync(oracle.fixture);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function verify(compiler, fixture, oracle, instantiate, makeWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        bytes.forEach((value, index) => tool.SetByte(array, index, value));
        return array;
    }
    const image = upload(new Uint8Array(fixture));
    const encoder = new TextEncoder();
    const manifest = [];
    for (const label of ["Process", "Thread", "ThreadId", "WindowThread", "Duplicate", "ExitCode", "ExitCodeOnStack",
        "Close", "Module", "SeedErrors", "SystemError", "SavedError"]) {
        const name = encoder.encode(label);
        const row = tool.FindRow(image, upload(name));
        if (row < 1) throw Error("Missing fixture method " + label);
        const header = new Uint8Array(8);
        const view = new DataView(header.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, name.length, true);
        manifest.push(...header, ...name);
    }
    const compiled = tool.CreateBytes(65536), location = tool.CreateDiagnostic();
    const length = tool.CompileModuleDetailed(image, upload(manifest), compiled, location);
    if (length <= 0)
        throw Error("Fixture status " + length + " at " + [0, 1, 2].map(index => tool.ReadDiagnostic(location, index)));
    const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(compiled, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind]);
    const wrongPointees = ["NarrowExit", "NarrowDuplicate"].map(label => {
        const row = tool.FindRow(image, upload(encoder.encode(label)));
        const status = tool.CompileDetailed(image, row, compiled, location);
        return row > 0 && status === -2;
    });
    async function launch() {
        const windows = makeWindows();
        let memory;
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (guest.Module() !== 1 || !memory) throw Error("Missing executable module or guest memory");
        return { guest, windows, memory };
    }

    const { guest, windows, memory } = await launch();
    const address = memory.grow(1) * 65536;
    let view = new DataView(memory.buffer);
    const guard = 0x76543210, output = address + 4;
    view.setUint32(address, guard, true);
    view.setUint32(address + 8, guard, true);
    const errors = source => ({ system: source.SystemError(), saved: source.SavedError() });
    const seed = source => source.SeedErrors(oracle.systemSeed, oracle.savedSeed);
    const process = guest.Process(), thread = guest.Thread();
    const threadId = guest.ThreadId();

    function exitStatus(handle, source = guest, pointer = output) {
        view.setUint32(output, oracle.exitSeed, true);
        seed(source);
        const ok = source.ExitCode(handle, pointer);
        return { ok, code: view.getUint32(output, true), ...errors(source) };
    }

    function duplicate(handle, destination = process, pointer = output) {
        view.setUint32(output, oracle.handleSeed, true);
        seed(guest);
        const ok = guest.Duplicate(process, handle, destination, pointer);
        const value = view.getUint32(output, true);
        return [value, { ok, nonzero: value !== 0, unchanged: value === oracle.handleSeed, ...errors(guest) }];
    }

    function close(handle) {
        seed(guest);
        const ok = guest.Close(handle);
        return { ok, ...errors(guest) };
    }

    const pseudo = exitStatus(thread);
    const [first, duplication] = duplicate(thread);
    const owned = exitStatus(first);
    const [second, duplicateAgain] = duplicate(first);
    const firstClose = close(first);
    const stale = exitStatus(first);
    const otherHandle = exitStatus(second);
    const invalid = exitStatus(0), processAsThread = exitStatus(process);
    const [, duplicateInvalid] = duplicate(0);
    const [, invalidDestination] = duplicate(thread, 0);
    const secondClose = close(second), closeTwice = close(second);
    const pseudoClose = close(thread), afterPseudoClose = exitStatus(thread);
    const [processCopy, ownedProcess] = duplicate(process);
    const processCopyAsThread = exitStatus(processCopy), closeProcessCopy = close(processCopy);
    const cases = {
        process, thread, currentIdStable: threadId > 0 && threadId === guest.ThreadId(),
        pseudo, duplicate: duplication, owned, duplicateAgain, firstClose, stale, otherHandle,
        invalid, processAsThread, duplicateInvalid, invalidDestination, secondClose, closeTwice, pseudoClose,
        afterPseudoClose, ownedProcess, processCopyAsThread, closeProcessCopy,
        stackExit: guest.ExitCodeOnStack(thread), stackInvalid: guest.ExitCodeOnStack(0)
    };

    // Native frame allocation can grow memory on the first stack-based call.
    view = new DataView(memory.buffer);
    const end = view.byteLength;
    const [, badDuplication] = duplicate(thread, process, end - 3);
    const invalidOutput = exitStatus(thread, guest, end - 3);
    const [foreignHandle] = duplicate(thread);
    const fresh = await launch();
    const foreign = exitStatus(foreignHandle, fresh.guest, 0);
    const safeguards = {
        distinctHandles: first !== second && second !== foreignHandle,
        sameWindowThread: guest.WindowThread(windows.register(0)) === threadId,
        distinctLaunchThread: fresh.guest.ThreadId() !== threadId,
        adjacentOutputUnchanged: view.getUint32(address, true) === guard && view.getUint32(address + 8, true) === guard,
        badDuplication, invalidOutput, foreign, wrongPointees
    };
    close(foreignHandle);
    return { imports, cases, safeguards };
}

const node = await verify(compiler, application, oracle, instantiateManagedModule, createWindowState);
assert.deepEqual(node.cases, oracle.cases);
for (const name of ["getCurrentProcess", "getCurrentThread", "getCurrentThreadId", "duplicateHandle",
    "getExitCodeThread", "closeHandle", "getWindowThreadProcessId"])
    assert.ok(node.imports.some(([module, entry, kind]) =>
        module === "standardwasm:win32" && entry === name && kind === "function"), name);
assert.deepEqual(node.safeguards, {
    distinctHandles: true, sameWindowThread: true, distinctLaunchThread: true, adjacentOutputUnchanged: true,
    badDuplication: { ok: 0, nonzero: true, unchanged: true, system: 998, saved: oracle.savedSeed },
    invalidOutput: { ok: 0, code: oracle.exitSeed, system: 998, saved: oracle.savedSeed },
    foreign: { ok: 0, code: oracle.exitSeed, system: 6, saved: oracle.savedSeed },
    wrongPointees: [true, true]
});

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route(server.url + "/fixture", route => route.fulfill({ body: application }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const inputs = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture"].map(async url => (await fetch(url)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, oracle, instantiateManagedModule, createWindowState);
    }, { source: verify.toString(), oracle });
    assert.deepEqual(edge, node);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), fixture: hash(application), oracle, nodeVersion: process.version,
        edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 across " + Object.keys(oracle.cases).length + " thread handle checks.");
} finally {
    await browser?.close();
    await server.close();
}
