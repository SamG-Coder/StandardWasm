import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { instantiateManagedModule } from "./resource-host.mjs";
import { createWindowState } from "./window-state.mjs";

const compiler = readFileSync(process.argv[2] || join(root, "artifacts", "StandardWasm.Compiler.wasm"));
const oracleFile = fileURLToPath(new URL("./Win32HostOracle.cs", import.meta.url));
const oracle = JSON.parse(execFileSync("dotnet", ["run", "--file", oracleFile, "--", "window-long"], { encoding: "utf8" }));
execFileSync("dotnet", ["build", fileURLToPath(new URL("./Win32Fixture", import.meta.url)), "-c", "Release"],
    { stdio: "inherit" });
const fixture = readFileSync(new URL("./Win32Fixture/bin/Release/net10.0/Win32Fixture.dll", import.meta.url));
const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
function upload(bytes) {
    const target = tool.CreateBytes(bytes.length);
    for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
    return target;
}
const entries = Buffer.concat(["ReadWindowLong", "ReadWindowLongError", "WriteWindowEnabled"].map(name => {
    const label = Buffer.from(name);
    const header = Buffer.alloc(8);
    const row = tool.FindRow(upload(fixture), upload(label));
    assert.ok(row > 0, name);
    header.writeInt32LE(row);
    header.writeInt32LE(label.length, 4);
    return Buffer.concat([header, label]);
}));
const output = tool.CreateBytes(65536), location = tool.CreateDiagnostic();
const size = tool.CompileModuleDetailed(upload(fixture), upload(entries), output, location);
assert.ok(size > 0, "Compiler status " + size + " at " + [0, 1, 2].map(index => tool.ReadDiagnostic(location, index)));
const code = Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index));
const module = await WebAssembly.compile(code);
const imports = WebAssembly.Module.imports(module);
assert.ok(imports.every(entry => entry.module === "standardwasm:win32"));
assert.deepEqual(imports.map(entry => entry.name).sort(), [
    "enableWindow", "getLastPInvokeError", "getLastSystemError", "getWindowLongPtrW", "getWindowLongW",
    "setLastPInvokeError", "setLastSystemError"
].sort());

async function verify(instantiate, makeWindows, code, oracle) {
    const windows = makeWindows();
    const handle = windows.register(oracle.style, null, {
        extendedStyle: oracle.extendedStyle, procedure: 1, instance: 1
    });
    const child = windows.register(0x40000000, null, { parent: handle, extraBytes: 8 });
    const { exports: api } = await instantiate(await WebAssembly.compile(code), undefined, [], {}, undefined, windows);
    const query = (window, index, mode) => [api.ReadWindowLong(window, index, mode), api.ReadWindowLongError()];
    const narrow = oracle.indexes.map(index => query(handle, index, 0));
    const pointer = [-4, -6, -16, -70, -21].map(index => query(handle, index, 1));
    const parent = query(child, -8, 1);
    const extra = [0, 4, 5].map(index => query(child, index, 1));
    api.WriteWindowEnabled(handle, 0);
    const disabled = query(handle, -16, 0);
    windows.unregister(handle);
    return { narrow, pointer, parent, extra, disabled, destroyed: query(handle, -16, 0) };
}

const node = await verify(instantiateManagedModule, createWindowState, code, oracle);
const expected = { narrow: oracle.narrow, pointer: oracle.pointer, parent: oracle.parent, disabled: oracle.disabled,
    destroyed: oracle.destroyed, extra: [[0, 0], [0, 0], [0, 1413]] };
assert.deepEqual(node, expected);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        if (path === "/scripts/native-imports.mjs")
            return route.fulfill({ contentType: "text/javascript", body: readFileSync(join(root, path)) });
        if (path !== "/stage-73-executable-intake/loader/pe.mjs" &&
            !["resource-host", "win32-host", "window-state", "activation-context", "dpi-awareness"]
                .some(name => path === "/stage-74-executable-entry/" + name + ".mjs")) return route.abort();
        return route.fulfill({ contentType: "text/javascript", body: readFileSync(join(root, "demos", path)) });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ code, oracle, source }) => {
        const { instantiateManagedModule } = await import("/stage-74-executable-entry/resource-host.mjs");
        const { createWindowState } = await import("/stage-74-executable-entry/window-state.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, new Uint8Array(code), oracle);
    }, { code: [...code], oracle, source: verify.toString() });
    assert.deepEqual(edge, expected);
    const directory = join(root, "artifacts", "window-long");
    mkdirSync(directory, { recursive: true });
    const evidence = mkdtempSync(join(directory, "run-"));
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        nodeVersion: process.version, edgeVersion: browser.version(), oracle, node, edge
    }, null, 2) + "\n");
    console.log("Windows, Node and Edge agree on window values and last-error transitions. " + evidence);
} finally { await browser.close(); }
