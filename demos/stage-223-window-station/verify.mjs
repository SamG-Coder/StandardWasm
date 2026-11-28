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

const stage = join(root, "demos/stage-223-window-station");
const evidence = join(root, "artifacts/stage-223-window-station");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = readFileSync(oracle.fixture);
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
    for (const name of ["Station", "ScalarStation", "Module", "Flags", "ScalarFlags", "SeedErrors", "SystemError", "SavedError"]) {
        const label = encoder.encode(name);
        const row = tool.FindRow(image, upload(label));
        if (row <= 0) throw Error("Missing fixture method " + name);
        const header = new Uint8Array(8);
        const view = new DataView(header.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, label.length, true);
        manifest.push(...header, ...label);
    }
    const output = tool.CreateBytes(1 << 16), diagnostic = tool.CreateDiagnostic();
    const size = tool.CompileModuleDetailed(image, upload(manifest), output, diagnostic);
    if (size <= 0)
        throw Error("Window station fixture status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index)));
    const imports = WebAssembly.Module.imports(module);
    const wrongRow = tool.FindRow(image, upload(encoder.encode("NarrowLength")));
    const narrowDiagnostic = tool.CompileDetailed(image, wrongRow, output, diagnostic);

    async function launch(desktop) {
        let memory;
        const windows = makeWindows();
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
            win32Desktop: desktop,
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (guest.Module() !== 1 || !memory) throw Error("The fixture requires the production guest memory.");
        const output = memory.grow(1) * 65536 + 1, needed = output + 20;
        const view = new DataView(memory.buffer);
        const bytes = new Uint8Array(memory.buffer, output, 16);
        return { guest, windows, output, needed, view, bytes };
    }

    function sample(context, handle, request, data = request.output ? context.output : 0,
        length = request.alias ? context.output + 8 : request.needed ? context.needed : 0) {
        const { guest, view, bytes, needed } = context;
        bytes.fill(0xa5);
        view.setUint32(needed, oracle.neededSeed, true);
        guest.SeedErrors(oracle.systemSeed, oracle.savedSeed);
        const query = request.scalar ? guest.ScalarFlags : guest.Flags;
        const ok = query(handle, request.index, data, request.size, length);
        return { ok, system: guest.SystemError(), saved: guest.SavedError(), needed: view.getUint32(needed, true),
            bytes: Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("").toUpperCase() };
    }

    const visible = await launch(true);
    const { guest } = visible;
    guest.SeedErrors(oracle.systemSeed, oracle.savedSeed);
    const station = guest.Station();
    const identity = { nonzero: station !== 0, stable: guest.Station() === station && guest.ScalarStation() === station,
        system: guest.SystemError(), saved: guest.SavedError() };
    const cases = Object.fromEntries(oracle.requests.map(request =>
        [request.name, sample(visible, request.handle ? station : 0, request)]));
    const headless = await launch(false);
    const headlessStation = headless.guest.Station();
    const flags = oracle.requests[0], limit = visible.view.byteLength;
    const hidden = sample(headless, headlessStation, flags);
    const foreign = sample(headless, station, flags);
    const windowAsStation = sample(visible, visible.windows.register(0), flags);
    const badOutput = sample(visible, station, flags, limit - 11);
    const badNeeded = sample(visible, station, flags, visible.output, limit - 3);
    let unsupported = false;
    try {
        sample(visible, station, { ...flags, index: 2 });
    } catch (error) {
        unsupported = error instanceof RangeError && error.message.includes("UOI_FLAGS");
    }
    const safeguards = { distinctStations: headlessStation !== 0 && headlessStation !== station,
        hidden, foreign, windowAsStation, badOutput, badNeeded, unsupported, narrowDiagnostic };
    return { imports, identity, cases, safeguards };
}

const node = await verify(compiler, fixture, oracle, instantiateManagedModule, createWindowState);
assert.deepEqual(node.identity, oracle.identity);
assert.deepEqual(node.cases, oracle.cases);
for (const name of ["getProcessWindowStation", "getUserObjectInformationW"])
    assert.ok(node.imports.some(entry => entry.module === "standardwasm:win32" && entry.name === name && entry.kind === "function"));
assert.equal(node.imports.filter(entry => entry.kind === "memory").length, 1);
assert.deepEqual(node.safeguards, {
    distinctStations: true,
    hidden: { ...oracle.cases.Flags, bytes: "000000000000000000000000A5A5A5A5" },
    foreign: oracle.cases.NullHandle, windowAsStation: oracle.cases.NullHandle,
    badOutput: oracle.cases.NullOutput, badNeeded: oracle.cases.NullOutput,
    unsupported: true, narrowDiagnostic: -2
});

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route(server.url + "/fixture", route => route.fulfill({ body: fixture }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const inputs = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture"]
            .map(async url => (await fetch(url)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, oracle, instantiateManagedModule, createWindowState);
    }, { source: verify.toString(), oracle });
    assert.deepEqual(edge, node);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), fixture: hash(fixture), clr: oracle.runtime, nodeVersion: process.version,
        edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the x86 Windows station identity and " +
        Object.keys(oracle.cases).length + " flags queries, and verified the browser safeguards.");
} finally {
    await browser?.close();
    await server.close();
}
