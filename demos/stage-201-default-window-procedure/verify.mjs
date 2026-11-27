import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import binaryen from "binaryen";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-201-default-window-procedure");
const evidence = join(root, "artifacts/stage-201-default-window-procedure");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, createWindows, bytes) {
    const module = new WebAssembly.Module(Uint8Array.from(bytes));
    function prepare(guest) {
        const base = guest.memory.grow(1) * 65536;
        const view = new DataView(guest.memory.buffer);
        function write(address, text, wide) {
            for (let i = 0; i < text.length; i++) {
                if (wide) view.setUint16(address + i * 2, text.charCodeAt(i), true);
                else view.setUint8(address + i, text.charCodeAt(i));
            }
        }
        const name = base + 64, upperName = base + 96, wideExport = base + 128, lowerExport = base + 160;
        const className = base + 192, caption = base + 256;
        write(name, "user32.dll", true);
        write(upperName, "USER32.DLL", true);
        write(wideExport, "DefWindowProcW", false);
        write(lowerExport, "defwindowprocw", false);
        write(className, "Stage201Popup", true);
        write(caption, "StandardWasm \u03bb", true);
        return { base, view, name, upperName, wideExport, lowerExport, className, caption };
    }

    const windows = createWindows();
    const signature = "stdcall:i32(p32,u32,u32,i32)";
    const messages = [];
    let procedure, fallback, addresses, registry;
    const { exports: guest } = await instantiate(module, {}, [], {}, "Fixture.exe", windows, undefined, {
        win32Desktop: true,
        configureNativeLibraries(libraries) {
            registry = libraries;
            libraries.registerLibrary("stage201-popup", new Map([["Procedure", { signature, invoke(hwnd, message, wparam, lparam) {
                const count = message === 0x81 || message === 1 ? 12 : message === 0x83 ? 4 : 0;
                const view = new DataView(guest.memory.buffer);
                const fields = () => Array.from({ length: count }, (_, i) => view.getInt32((lparam >>> 0) + i * 4, true));
                const before = fields();
                const result = message === 0x81 && !fallback ? 1 : guest.callWindowProcW(procedure, hwnd, message, wparam, lparam);
                const unchanged = fields().every((value, i) => value === before[i]);
                messages.push({ message, wparam, packed: count ? 0 : lparam, result, unchanged });
                return result;
            } }]]));
        }
    });
    addresses = prepare(guest);
    const resident = guest.getModuleHandleW(addresses.name);
    const sameModule = guest.getModuleHandleW(addresses.upperName);
    procedure = guest.getProcAddress(resident, addresses.wideExport);
    const sameProc = guest.getProcAddress(sameModule, addresses.wideExport);
    const wrongCase = guest.getProcAddress(resident, addresses.lowerExport);
    const wrongCaseError = guest.getLastSystemError();
    if (!resident || !procedure) throw Error("Missing built-in USER32.dll or DefWindowProcW");

    const owned = registry.loadLibrary("user32.dll");
    const residentSurvivesRelease = owned === resident && registry.freeLibrary(owned) === 1 &&
        guest.getModuleHandleW(addresses.name) === resident && guest.getProcAddress(resident, addresses.wideExport) === procedure;
    const callback = registry.getProcAddress(registry.loadLibrary("stage201-popup"), "Procedure");
    [0, callback, 0, 0, 1, 0, 0, 0, 0, addresses.className].forEach((field, index) =>
        addresses.view.setInt32(addresses.base + index * 4, field, true));
    const atom = guest.registerClassW(addresses.base);
    if (!atom) throw Error("Could not register test popup");
    const create = () => guest.createWindowExW(0, atom, addresses.caption, 0x80000000 | 0, 110, 70, 320, 144, 0, 0, 1, 0x1234567);
    fallback = true;
    const hwnd = create();
    if (!hwnd) throw Error("Default procedure did not allow popup creation");
    const observed = { moduleLoaded: resident !== 0, moduleStable: sameModule === resident, procedureStable: sameProc === procedure,
        wrongCase, wrongCaseError, title: windows.getWindowTitle(hwnd), messages: messages.slice() };

    fallback = false;
    const manual = create();
    const manualTitleEmpty = manual !== 0 && windows.getWindowTitle(manual) === "";
    let rejectedMessage = false, rejectedPointer = false;
    try { guest.callWindowProcW(procedure, hwnd, 0x1234, 0, 0); }
    catch (error) { rejectedMessage = /unsupported default window message/u.test(error.message); }
    try { guest.callWindowProcW(procedure, hwnd, 0x81, 0, -1); }
    catch (error) { rejectedPointer = /invalid guest buffer/u.test(error.message); }

    const { exports: isolated } = await instantiate(module, {}, [], {}, "Fixture.exe");
    const other = prepare(isolated);
    const nonDesktop = isolated.getModuleHandleW(other.name) === 0 && isolated.getLastSystemError() === 126;
    const { exports: replacement } = await instantiate(module, {}, [], {}, "Fixture.exe", undefined, undefined, {
        win32Desktop: true,
        configureNativeLibraries(libraries) {
            libraries.registerLibrary("USER32.dll", new Map([["DefWindowProcW", { signature, invoke: () => 73 }]]), true);
        }
    });
    const custom = prepare(replacement);
    const selected = replacement.getProcAddress(replacement.getModuleHandleW(custom.name), custom.wideExport);
    const explicitProviderRetained = replacement.callWindowProcW(selected, 0, 0, 0, 0) === 73;
    let isolatedToken = false;
    try { replacement.callWindowProcW(procedure, 0, 0, 0, 0); }
    catch (error) { isolatedToken = /unregistered dynamic export token/u.test(error.message); }
    return { observed, residentSurvivesRelease, manualTitleEmpty, rejectedMessage, rejectedPointer,
        nonDesktop, explicitProviderRetained, isolatedToken };
}

const node = await exercise(instantiateManagedModule, createWindowState, bytes);
assert.deepEqual(node.observed, oracle);
assert.deepEqual({ ...node, observed: undefined }, {
    observed: undefined, residentSurvivesRelease: true, manualTitleEmpty: true, rejectedMessage: true,
    rejectedPointer: true, nonDesktop: true, explicitProviderRetained: true, isolatedToken: true
});
const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ code, bytes }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + code + ")")(instantiateManagedModule, createWindowState, bytes);
    }, { code: exercise.toString(), bytes: [...bytes] });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        clr: runtime.version, nodeVersion: process.version, edgeVersion: browser.version(), oracle, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 default window procedure for hidden popup creation.");
} finally {
    await browser?.close();
    await server.close();
}
