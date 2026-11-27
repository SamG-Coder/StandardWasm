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

const stage = join(root, "demos/stage-318-message-only-windows");
const evidence = join(root, "artifacts/stage-318-message-only-windows");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, createWindows, bytes, metrics) {
    const module = new WebAssembly.Module(Uint8Array.from(bytes));
    async function launch(windowCreationMetrics) {
        const windows = createWindows();
        let registry, guest, fallback, root, active;
        const signature = "stdcall:i32(p32,u32,u32,i32)";
        const { exports } = await instantiate(module, {}, [], {}, "Fixture.exe", windows, undefined, {
            win32Desktop: true,
            windowCreationMetrics,
            configureNativeLibraries(libraries) {
                registry = libraries;
                libraries.registerLibrary("stage318", new Map([["Procedure", { signature, invoke: dispatch }]]));
            }
        });
        guest = exports;
        const base = guest.memory.grow(1) * 65536;
        const className = base + 64, caption = base + 128, user32 = base + 192, symbol = base + 224;
        const view = new DataView(guest.memory.buffer);
        const writeWide = (address, text) => {
            for (let i = 0; i < text.length; i++) view.setUint16(address + 2 * i, text.charCodeAt(i), true);
        };
        writeWide(className, "StandardWasm.MessageOnly");
        writeWide(caption, "Parking");
        writeWide(user32, "user32.dll");
        new Uint8Array(guest.memory.buffer).set(new TextEncoder().encode("DefWindowProcW"), symbol);
        fallback = guest.getProcAddress(guest.getModuleHandleW(user32), symbol);
        const proc = registry.getProcAddress(registry.loadLibrary("stage318"), "Procedure");
        [0, proc, 0, 0, 1, 0, 0, 0, 0, className].forEach((value, i) => view.setInt32(base + i * 4, value, true));
        const atom = guest.registerClassW(base);
        if (!atom || !fallback) throw Error("Missing class or production default WNDPROC");

        function parentKind(parent) {
            return parent === 0 ? "none" : parent === -3 ? "sentinel" :
                parent === root && windows.hasWindow(parent) ? "system" : "unknown";
        }

        function dispatch(hwnd, message, wParam, lParam) {
            if (!active) return guest.callWindowProcW(fallback, hwnd, message, wParam, lParam);
            active.handle ??= hwnd;
            const view = new DataView(guest.memory.buffer);
            const read = count => Array.from({ length: count }, (_, i) => view.getInt32(lParam + i * 4, true));
            const parentField = guest.getWindowLongW(hwnd, -8);
            root ??= parentField;
            let data = [], parent = null, caption = null;
            if (message === 0x24) data = read(10);
            if (message === 0x83) data = read(4);
            if (message === 0x81 || message === 1) {
                const fields = read(12);
                data = [fields[0], Number(fields[1] === 1), fields[2], ...fields.slice(4, 9), fields[11]];
                parent = parentKind(fields[3]);
                caption = "";
                for (let at = fields[9], unit; (unit = view.getUint16(at, true)) !== 0; at += 2)
                    caption += String.fromCharCode(unit);
            }
            const observed = { message, wParam, data, parent, caption, live: windows.hasWindow(hwnd),
                style: guest.getWindowLongW(hwnd, -16) >>> 0, exStyle: guest.getWindowLongW(hwnd, -20) >>> 0,
                parentField: parentKind(parentField) };
            const result = active.abort && message === 0x81 ? 0 : guest.callWindowProcW(fallback, hwnd, message, wParam, lParam);
            active.messages.push({ ...observed, adjusted: message === 0x83 ? read(4) : [] });
            return result;
        }

        function create(name, named, style, x, y, width, height, abort = false) {
            active = { abort, messages: [] };
            guest.setLastSystemError(0);
            const hwnd = guest.createWindowExW(0, named ? className : atom, caption, style, x, y, width, height, -3, 0, 1, 123);
            const { handle, messages } = active;
            active = undefined;
            return { hwnd, result: { name, success: hwnd !== 0, error: hwnd === 0 ? guest.getLastSystemError() : 0,
                live: windows.hasWindow(handle), sameHandle: hwnd === 0 || hwnd === handle,
                visible: hwnd !== 0 && (!windows.isMessageOnlyWindow(hwnd) ||
                    (guest.getWindowLongW(hwnd, -16) & 0x10000000) !== 0),
                messages } };
        }
        return { create, guest, windows, root: () => root };
    }

    const desktop = await launch(metrics);
    const parking = desktop.create("parking", true, 0x02010000, 0, 0, 0, 0);
    const sibling = desktop.create("sibling", false, 0, 2, 3, 200, 85);
    const rejected = desktop.create("reject", true, 0x02010000, 0, 0, 0, 0, true);
    const initial = desktop.guest.getFocus();
    const previous = desktop.guest.setFocus(parking.hwnd);
    const focused = desktop.guest.getFocus() === parking.hwnd;
    desktop.guest.setFocus(initial);
    const other = await launch();
    const independent = !other.windows.hasWindow(parking.hwnd) && !other.windows.hasWindow(desktop.root());
    const defaultParking = other.create("default-metrics", true, 0x02010000, 0, 0, 0, 0);
    return { cases: [parking.result, sibling.result, rejected.result],
        focus: { hadFocus: initial !== 0, previous: previous !== 0, focused },
        independent, title: desktop.windows.getWindowTitle(parking.hwnd),
        defaultParking: { success: defaultParking.result.success, visible: defaultParking.result.visible,
            messages: defaultParking.result.messages.map(item => item.message) } };
}

const node = await exercise(instantiateManagedModule, createWindowState, bytes, oracle.metrics);
assert.deepEqual(node.cases, oracle.cases);
assert.deepEqual(node.focus, oracle.focus);
assert.equal(node.independent, true);
assert.equal(node.title, "Parking");
assert.deepEqual(node.defaultParking, { success: true, visible: false, messages: [0x24, 0x81, 0x83, 1] });
const server = await serveLaunch(0, new Uint8Array());
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, bytes, metrics }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, bytes, metrics);
    }, { source: exercise.toString(), bytes: [...bytes], metrics: oracle.metrics });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, edge, nodeVersion: process.version, edgeVersion: browser.version()
    }, null, 2) + "\n");
    console.log("Windows x86, Node and Edge matched message-only creation and rejection.");
} finally {
    await browser.close();
    await server.close();
}
