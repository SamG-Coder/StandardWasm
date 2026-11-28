import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-236-class-window-procedure");
const evidence = join(root, "artifacts/stage-236-class-window-procedure");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/ClassWindowProcedure.dll"));

async function verify({ compiler, image }, instantiate, createWindows) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    const procSignature = "stdcall:i32(p32,u32,u32,i32)", echo = 0x8042;
    const classProcedure = -24, windowProcedure = -4;
    const systemSentinel = 17185, savedSentinel = 25090;
    const windows = createWindows();
    const encoder = new TextEncoder();
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const input = upload(decode(image));
    function compile(names) {
        const manifest = [];
        for (const name of names) {
            const id = encoder.encode(name), row = tool.FindRow(input, upload(id));
            if (row < 1) throw Error("Missing fixture export " + name);
            const entry = new Uint8Array(8 + id.length), view = new DataView(entry.buffer);
            view.setUint32(0, row, true);
            view.setUint32(4, id.length, true);
            entry.set(id, 8);
            manifest.push(...entry);
        }
        const output = tool.CreateBytes(1 << 18), diagnostic = tool.CreateDiagnostic();
        const size = tool.CompileModuleDetailed(input, upload(manifest), output, diagnostic);
        if (size < 1) throw Error("Compiler status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        return new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    }
    const setters = ["SetClass", "SetClassPointer"];
    const rawImports = WebAssembly.Module.imports(compile(setters));
    const module = compile([...setters, "Register", "ClassInfo", "Create", "SetWindow", "GetWindow", "SystemError", "SavedError", "SetErrors"]);

    async function launch() {
        let memory, original, replacement, wrongType;
        const { exports: app } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
            configureNativeLibraries(registry) {
                memory = registry.memory;
                function callback(value) {
                    return { signature: procSignature, invoke(_window, message) {
                        return message === 0x0081 ? 1 : message === echo ? value : 0;
                    }};
                }
                registry.registerLibrary("fixture-user32", new Map([
                    ["Original", callback(101)], ["Replacement", callback(202)],
                    ["WrongType", { signature: "stdcall:i32(p32)", invoke() { throw Error("Unexpected callback"); } }]
                ]));
                const library = registry.loadLibrary("fixture-user32");
                original = registry.getProcAddress(library, "Original");
                replacement = registry.getProcAddress(library, "Replacement");
                wrongType = registry.getProcAddress(library, "WrongType");
            }
        });
        const definition = memory.grow(1) * 65536, output = definition + 48, name = definition + 96;
        const view = new DataView(memory.buffer), text = "StandardWasm.Stage236";
        for (let index = 0; index < text.length; index++) view.setUint16(name + 2 * index, text.charCodeAt(index), true);
        const fields = [0, original, 0, 0, 1, 0, 0, 0, 0, name];
        fields.forEach((value, index) => view.setInt32(definition + 4 * index, value, true));
        const atom = app.Register(definition);
        if (!atom) throw Error("RegisterClassW failed: " + app.SystemError());
        return {
            app, original, replacement, wrongType, name, atom,
            open(identifier) {
                const hwnd = app.Create(identifier, 1);
                if (!hwnd) throw Error("CreateWindowExW failed: " + app.SystemError());
                return hwnd;
            },
            classReports(procedure) {
                return !!app.ClassInfo(1, name, output) && new DataView(memory.buffer).getInt32(output + 4, true) === procedure;
            },
            capture(action, hwnd, index, procedure) {
                app.SetErrors(systemSentinel, savedSentinel);
                const previous = action(hwnd, index, procedure);
                return { previous, system: app.SystemError(), saved: app.SavedError() };
            }
        };
    }

    const main = await launch(), { app, original, replacement } = main;
    const a = main.open(main.name), b = main.open(main.atom);
    const message = hwnd => windows.dispatchWindowMessage(hwnd, echo, 0, 0);
    const before = [message(a), message(b)];
    const oldWindowWasOriginal = app.SetWindow(a, windowProcedure, replacement) === original;
    const changed = main.capture(app.SetClass, a, classProcedure, replacement);
    const after = [message(a), message(b)];
    const c = main.open(main.name);
    const infoReadsChangedProcedure = main.classReports(replacement);
    const oldWindowProcedureUnchanged = app.GetWindow(b, windowProcedure) === original;
    // On wasm32 both original native leaves use the same raw import.
    const restored = main.capture(app.SetClassPointer, b, classProcedure, original);
    const d = main.open(main.atom);
    const result = ({ previous, system, saved }) => ({ zero: previous === 0, system, saved });
    const error = (window, index, token) => result(main.capture(app.SetClass, window, index, token));
    const badIndex = error(a, -999, original), badWindow = error(0, classProcedure, original);
    const wrongSignature = error(a, classProcedure, main.wrongType);
    const isolated = await launch();
    // Share the public window registry to prove that class ownership still
    // limits mutations even when a caller can identify the other launch's HWND.
    const foreignClass = result(isolated.capture(isolated.app.SetClass, a, classProcedure, isolated.replacement));
    windows.unregister(b);
    const staleWindow = error(b, classProcedure, replacement);
    return {
        rawImports, results: {
            before, after, newAfterChange: message(c), newAfterRestore: message(d), cAfterRestore: message(c),
            oldWindowWasOriginal, oldWindowProcedureUnchanged, infoReadsChangedProcedure,
            classReturnCorrect: changed.previous === original, classRestoreReturnCorrect: restored.previous === replacement,
            changedErrors: [changed.system, changed.saved], restoredErrors: [restored.system, restored.saved], badIndex, badWindow
        },
        browserChecks: {
            wrongSignature, foreignClass, staleWindow,
            ownedClassIntact: main.classReports(original), isolatedClassIntact: isolated.classReports(isolated.original)
        }
    };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64") };
const node = await verify(input, instantiateManagedModule, createWindowState);
assert.deepEqual(node.rawImports, [{ module: "standardwasm:win32", name: "setClassLongW", kind: "function" }]);
assert.deepEqual(node.results, oracle.results);
assert.deepEqual(node.browserChecks, {
    wrongSignature: { zero: true, system: 87, saved: 25090 },
    foreignClass: { zero: true, system: 1400, saved: 25090 },
    staleWindow: { zero: true, system: 1400, saved: 25090 },
    ownedClassIntact: true, isolatedClassIntact: true
});

const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule, createWindowState);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(image), clr: oracle.clr, oracle: oracle.results,
        nodeVersion: process.version, edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 CLR on class WNDPROC replacement and checked browser token isolation.");
} finally {
    await browser?.close();
    await server.close();
}
