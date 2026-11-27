import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-190-window-creation-binding");
const evidence = join(root, "artifacts/stage-190-window-creation-binding");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
run("dotnet", ["build", "-c", "Release", join(stage, "Fixture/WindowCreationBinding.csproj")]);
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/WindowCreationBinding.dll"));

async function verify({ compiler, image }, instantiate, createWindows) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }

    const input = upload(decode(image));
    const encoder = new TextEncoder();
    function compile(names) {
        const entries = [];
        for (const name of names) {
            const id = encoder.encode(name);
            const row = tool.FindRow(input, upload(id));
            if (row < 1) throw Error("Missing fixture entry " + name);
            const entry = new Uint8Array(8 + id.length);
            const view = new DataView(entry.buffer);
            view.setUint32(0, row, true);
            view.setUint32(4, id.length, true);
            entry.set(id, 8);
            entries.push(...entry);
        }
        const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
        const size = tool.CompileModuleDetailed(input, upload(entries), output, diagnostic);
        if (size < 1)
            throw Error("Compiler status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        return new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    }
    const module = compile(["Create", "CreateDefaults"]);
    const calls = [];
    const guest = new WebAssembly.Instance(module, {
        "standardwasm:resources": { memory: new WebAssembly.Memory({ initial: 1 }) },
        "standardwasm:win32": { createWindowExW(...args) {
            calls.push(args);
            return calls.length === 1 ? 0x76543210 : 0;
        } }
    }).exports;
    const args = [0x80000001 | 0, 65530, 123456, 0xf1000004 | 0, -5, 6, 601, 402, -3, 77, 88, 0x88550099 | 0];
    const windows = createWindows();
    const messages = [];
    let memory, procedure;
    const production = compile(["Create", "CreateDefaults", "Register", "SystemError"]);
    const { exports: app } = await instantiate(production, tool, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries(registry) {
            memory = registry.memory;
            registry.registerLibrary("stage190", new Map([["Procedure", {
                signature: "stdcall:i32(p32,u32,u32,i32)",
                invoke(hwnd, message, wparam, lparam) {
                    const createData = message === 0x81 || message === 1
                        ? new DataView(memory.buffer).getInt32(lparam >>> 0, true)
                        : null;
                    messages.push({ hwnd, message, wparam, createData, live: windows.hasWindow(hwnd) });
                    return message === 0x81 ? 1 : 0;
                }
            }]]));
            procedure = registry.getProcAddress(registry.loadLibrary("stage190"), "Procedure");
        }
    });
    const registration = memory.grow(1) * 65536, className = registration + 64;
    const view = new DataView(memory.buffer);
    const text = "StandardWasm.Stage190";
    for (let i = 0; i < text.length; i++) view.setUint16(className + 2 * i, text.charCodeAt(i), true);
    [0, procedure, 0, 0, 1, 0, 0, 0, 0, className].forEach((value, i) => view.setInt32(registration + 4 * i, value, true));
    const atom = app.Register(registration);
    const hwnd = app.Create(0, atom, 0, 0x80000000 | 0, 10, 20, 100, 90, 0, 0, 1, 0x318);
    const missing = app.CreateDefaults();
    return {
        args, calls, handle: guest.Create(...args), failedHandle: guest.CreateDefaults(),
        imports: WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind]),
        production: { registered: atom !== 0, hwnd, messages, missing, systemError: app.SystemError() }
    };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64") };
const node = await verify(input, instantiateManagedModule, createWindowState);
assert.deepEqual(node.calls, [node.args, Array(12).fill(0)]);
assert.equal(node.handle, 0x76543210);
assert.equal(node.failedHandle, 0);
assert.deepEqual(node.imports, [
    ["standardwasm:resources", "memory", "memory"],
    ["standardwasm:win32", "createWindowExW", "function"]
]);
const { hwnd, messages, ...production } = node.production;
assert.notEqual(hwnd, 0);
assert.deepEqual(production, { registered: true, missing: 0, systemError: 1407 });
assert.deepEqual(messages, [0x81, 0x83, 1, 5, 3].map(message => ({
    hwnd, message, wparam: 0, createData: message === 0x81 || message === 1 ? 0x318 : null, live: true
})));
const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: process.env.STANDARDWASM_BROWSER_CHANNEL || "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule, createWindowState);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    mkdirSync(evidence, { recursive: true });
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"),
        nodeVersion: process.version, browserVersion: browser.version(), node, edge
    }, null, 2));
    console.log("Node and Edge passed CreateWindowExW ABI forwarding and production host window creation.");
} finally {
    await browser?.close();
    await server.close();
}
