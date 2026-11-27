import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root, run } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-187-window-class-bindings");
const evidence = join(root, "artifacts/stage-187-window-class-bindings");
mkdirSync(evidence, { recursive: true });
run("dotnet", ["build", "-c", "Release", join(stage, "Fixture/WindowClassBindings.csproj")]);
const oracleStage = join(root, "demos/stage-184-window-class-registration/Oracle");
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(oracleStage, "Oracle.csproj"),
    join(oracleStage, "bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const assembly = readFileSync(join(stage, "Fixture/bin/Release/net10.0/WindowClassBindings.dll"));

async function exercise(instantiate, createWindows, compiler, assembly) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }

    const image = upload(new Uint8Array(assembly));
    const encoder = new TextEncoder();
    const entries = [];
    for (const name of ["Register", "Lookup", "Replace", "ReplacePointer", "Read", "SavedError"]) {
        const id = encoder.encode(name);
        const row = tool.FindRow(image, upload(id));
        if (row < 1) throw Error("Missing fixture entry " + name);
        const entry = new Uint8Array(8 + id.length);
        const view = new DataView(entry.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, id.length, true);
        entry.set(id, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const size = tool.CompileModuleDetailed(image, upload(entries), output, diagnostic);
    if (size < 1)
        throw Error("Compiler status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const windows = createWindows();
    let memory, original, replacement;
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries(registry) {
            memory = registry.memory;
            const callback = { signature: "stdcall:i32(p32,u32,u32,i32)", invoke: () => 0 };
            registry.registerLibrary("fixture-user32", new Map([["Original", callback], ["Replacement", callback]]));
            const library = registry.loadLibrary("fixture-user32");
            original = registry.getProcAddress(library, "Original");
            replacement = registry.getProcAddress(library, "Replacement");
        }
    });
    const definition = memory.grow(1) * 65536, outputAddress = definition + 48, nameAddress = definition + 128;
    const name = "StandardWasm.Stage187";
    const view = new DataView(memory.buffer);
    for (let i = 0; i < name.length; i++) view.setUint16(nameAddress + i * 2, name.charCodeAt(i), true);
    const fields = [3, original, 4, 8, 1, 0, 0, 0, 7, nameAddress];
    fields.forEach((value, index) => view.setInt32(definition + 4 * index, value, true));
    const atom = guest.Register(definition);
    const duplicate = guest.Register(definition);
    const named = guest.Lookup(1, nameAddress, outputAddress);
    const namePointerMatches = view.getInt32(outputAddress + 36, true) === nameAddress;
    const byAtom = guest.Lookup(1, atom, outputAddress);
    const attributes = Array.from({ length: 9 }, (_, index) => view.getInt32(outputAddress + 4 * index, true));
    attributes[1] = attributes[1] === original;
    const atomPointerMatches = view.getInt32(outputAddress + 36, true) === atom;
    const window = windows.register(0, undefined, { procedure: original });
    const previous = guest.Replace(window, -4, replacement);
    const changed = guest.Read(window, -4);
    const restored = guest.ReplacePointer(window, -4, original);
    const current = guest.Read(window, -4);
    const invalid = guest.Replace(window, -1000, replacement);
    return { registered: atom !== 0, duplicate, namedEqualsAtom: named === atom, atomEqualsAtom: byAtom === atom,
        namePointerMatches, atomPointerMatches, attributes, previousMatches: previous === original,
        currentMatches: changed === replacement, restored: restored === replacement && current === original,
        invalid, invalidError: guest.SavedError(),
        imports: WebAssembly.Module.imports(module).map(({ module, name }) => module + "/" + name) };
}

const node = await exercise(instantiateManagedModule, createWindowState, compiler, assembly);
const expected = oracle.outcomes;
const { style, original, classExtra, windowExtra, module, icon, cursor, background, menu } = expected.namedInfo;
assert.deepEqual(node, {
    registered: expected.registered, duplicate: expected.duplicate, namedEqualsAtom: expected.namedEqualsAtom,
    atomEqualsAtom: expected.atomEqualsAtom, namePointerMatches: expected.namedInfo.name === "query name",
    atomPointerMatches: expected.atomInfo.name === "query atom",
    attributes: [style, original, classExtra, windowExtra, module ? 1 : 0, icon, cursor, background, menu],
    previousMatches: expected.previousMatches, currentMatches: expected.currentMatches, restored: true,
    invalid: expected.badIndex, invalidError: expected.badIndexError,
    imports: ["standardwasm:resources/memory", ...["getLastSystemError", "setLastSystemError", "getLastPInvokeError",
        "setLastPInvokeError", "getWindowLongW", "registerClassW", "getClassInfoW", "setWindowLongW", "setWindowLongPtrW"]
        .map(name => "standardwasm:win32/" + name)]
});

const server = await serveLaunch(0, new Uint8Array());
const edge = await chromium.launch({ channel: "msedge" });
try {
    const page = await edge.newPage();
    await page.route(server.url + "/compiler", route => route.fulfill({ body: compiler }));
    await page.route(server.url + "/class-fixture", route => route.fulfill({ body: assembly }));
    await page.goto(server.url);
    const browser = await page.evaluate(async source => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/compiler", "/class-fixture"].map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, ...assets);
    }, exercise.toString());
    assert.deepEqual(browser, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"),
        clr: oracle.clr, nodeVersion: process.version, edgeVersion: edge.version(), node, browser
    }, null, 2) + "\n");
    console.log("Compiled SDK-shaped Win32 class and procedure bindings matched the Windows x86 oracle in Node and Edge.");
} finally {
    await edge.close();
    await server.close();
}
