import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-361-enum-thread-windows");
const evidence = join(root, "artifacts/stage-361-enum-thread-windows");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, image, instantiate, createRegistry) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(target, index, bytes[index]);
        return target;
    }
    const source = upload(image);
    const encoder = new TextEncoder();
    const row = name => tool.FindRow(source, upload(encoder.encode(name)));
    const entries = [];
    for (const name of ["Query", "Count", "Payload", "HandleAt", "EscapeCallback"]) {
        const text = encoder.encode(name);
        const entry = new Uint8Array(text.length + 8), view = new DataView(entry.buffer);
        const method = row(name);
        if (method <= 0) throw Error("Missing " + name);
        view.setUint32(0, method, true);
        view.setUint32(4, text.length, true);
        entry.set(text, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileModuleDetailed(source, upload(entries), output, diagnostic);
    if (size <= 0) throw Error("Fixture compilation " + size + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name, kind }) => module + "/" + name + "/" + kind).sort();
    const required = ["standardwasm:win32/enumThreadWindows/function"];
    if (imports.join() !== required.join()) throw Error("Unexpected imports " + imports);
    if (WebAssembly.Module.exports(module).some(item => item.kind === "table"))
        throw Error("The callback table must remain private.");

    const bad = row("RejectCallingConvention"), target = row("WrongCallingConvention");
    if (bad < 1 || target < 1) throw Error("Missing cdecl rejection fixture");
    const rejection = { status: tool.CompileDetailed(source, bad, output, diagnostic), location: location() };
    if (rejection.status !== -2 || rejection.location.join() !== [0, target, -1].join())
        throw Error("Expected a located diagnostic for a cdecl callback.");

    const windows = createRegistry();
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows);
    const thread = windows.getCurrentThreadId(), payload = 0xd1234567 | 0;
    const labels = new Map(), observations = {};
    function observe(name, owner, limit) {
        const result = guest.Query(owner, payload, limit);
        const visited = Array.from({ length: guest.Count() }, (_, i) => labels.get(guest.HandleAt(i)));
        observations[name] = { result, visited, payload: guest.Payload() };
    }
    function register(name, style, attributes) {
        const hwnd = windows.register(style, undefined, attributes);
        labels.set(hwnd, name);
        return hwnd;
    }
    observe("empty", thread, 4);
    const first = register("first", 0x80000000);
    register("child", 0x40000000, { parent: first });
    register("second", 0x80000000);
    register("third", 0x80000000);
    register("message", 0, { parent: windows.getMessageRoot(), messageOnly: true });
    observe("all", thread, 4);
    observe("stopFirst", thread, 1);
    observe("stopSecond", thread, 2);
    observe("invalidThread", 0xffffffff, 4);
    windows.unregister(first);
    observe("destroyedWindows", thread, 4);

    // An escaping exception ends the launch as the CLR's fail-fast does: a trap that
    // managed catch cannot intercept, instead of unwinding into EnumThreadWindows' caller.
    let escaped;
    try { escaped = { result: guest.EscapeCallback(thread) }; }
    catch (error) { escaped = { trap: error instanceof WebAssembly.RuntimeError, message: error.message }; }
    if (escaped.trap !== true) throw Error("An exception escaping the callback must trap: " + JSON.stringify(escaped));

    const foreign = createRegistry();
    foreign.register(0x80000000);
    observe("foreignThread", foreign.getCurrentThreadId(), 4);
    const isolated = await instantiate(module, tool, [], {}, "Fixture.exe", foreign);
    if (isolated.exports.Query(thread, payload, 4) !== 1 || isolated.exports.Count() !== 0)
        throw Error("One launch saw another launch's thread.");

    register("shown", 0x90000000);
    let mixedOrder;
    try { guest.Query(thread, payload, 4); } catch (error) { mixedOrder = error.message; }
    if (!mixedOrder?.includes("requires z-order tracking")) throw Error("Expected explicit mixed z-order diagnostic.");
    return { observations, imports, rejection, mixedOrder, escaped: escaped.trap };
}

const node = await exercise(compiler, image, instantiateManagedModule, createWindowState);
assert.deepEqual(node.observations, { ...oracle.observations, foreignThread: oracle.observations.invalidThread });

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/fixture.dll", route => route.fulfill({ body: image, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async code => {
        const [{ instantiateManagedModule }, { createWindowState }] = await Promise.all([
            import("/launch/resource-host.mjs"), import("/launch/window-state.mjs")
        ]);
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + code + ")")(...assets, instantiateManagedModule, createWindowState);
    }, exercise.toString());
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const report = { compiler: sha256(compiler), assembly: sha256(image),
        clr: oracle.runtime + "/" + oracle.architecture, windows: oracle.os, node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify(report));
} finally {
    await browser?.close();
    await server.close();
}
