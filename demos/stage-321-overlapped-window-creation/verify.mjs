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

const stage = join(root, "demos/stage-321-overlapped-window-creation");
const evidence = join(root, "artifacts/stage-321-overlapped-window-creation");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "../stage-318-message-only-windows/abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, createWindows, bytes, { requests, metrics }) {
    const module = new WebAssembly.Module(Uint8Array.from(bytes));
    async function launch(windowCreationMetrics) {
        const windows = createWindows();
        const signature = "stdcall:i32(p32,u32,u32,i32)";
        let guest, registry, fallback, active;
        const { exports } = await instantiate(module, {}, [], {}, "Fixture.exe", windows, undefined, {
            win32Desktop: true,
            windowCreationMetrics,
            configureNativeLibraries(libraries) {
                registry = libraries;
                libraries.registerLibrary("stage321", new Map([["Procedure", { signature, invoke: dispatch }]]));
            }
        });
        guest = exports;
        const base = guest.memory.grow(1) * 65536;
        const className = base + 64, caption = base + 192, user32 = base + 256, symbol = base + 320;
        const view = new DataView(guest.memory.buffer);
        function wide(address, text) {
            for (let i = 0; i < text.length; i++) view.setUint16(address + i * 2, text.charCodeAt(i), true);
        }
        wide(className, "StandardWasm.OverlappedCreation");
        wide(caption, "Overlapped creation");
        wide(user32, "user32.dll");
        new Uint8Array(guest.memory.buffer).set(new TextEncoder().encode("DefWindowProcW"), symbol);
        fallback = guest.getProcAddress(guest.getModuleHandleW(user32), symbol);
        const callback = registry.getProcAddress(registry.loadLibrary("stage321"), "Procedure");
        [0, callback, 0, 0, 1, 0, 0, 0, 0, className].forEach((value, i) => view.setInt32(base + i * 4, value, true));
        const atom = guest.registerClassW(base);
        if (!atom || !fallback) throw Error("Could not register the guest window class");

        function dispatch(hwnd, message, wParam, lParam) {
            if (!active) throw Error("Unexpected window callback");
            active.handle ??= hwnd;
            const view = new DataView(guest.memory.buffer);
            const read = count => Array.from({ length: count }, (_, i) => view.getInt32(lParam + i * 4, true));
            let data = [];
            if (message === 0x24) data = read(10);
            else if (message === 0x83) data = read(4);
            else if (message === 0x81 || message === 1) {
                const fields = read(12);
                data = [fields[0], Number(fields[1] === 1), ...fields.slice(2, 9), fields[11]];
            }
            const style = guest.getWindowLongW(hwnd, -16) >>> 0, exStyle = guest.getWindowLongW(hwnd, -20) >>> 0;
            const result = active.reject && message === 0x81 ? 0 : guest.callWindowProcW(fallback, hwnd, message, wParam, lParam);
            active.messages.push({ message, wParam, data, result, adjusted: message === 0x83 ? read(4) : [], style, exStyle });
            return result;
        }

        function create({ ExStyle, Style, X, Y, Width, Height, Reject }) {
            active = { reject: Reject, messages: [] };
            guest.setLastSystemError(0);
            const hwnd = guest.createWindowExW(ExStyle, atom, caption, Style, X, Y, Width, Height, 0, 0, 1, 123);
            const error = hwnd === 0 ? guest.getLastSystemError() : 0;
            const { handle, messages } = active;
            active = undefined;
            const geometry = windows.getWindowGeometry(hwnd);
            let window = [], client = [], clientOrigin = [];
            if (geometry) {
                window = geometry.outer;
                const [left, top, right, bottom] = geometry.client;
                client = [0, 0, right - left, bottom - top];
                clientOrigin = [left, top];
            }
            return { hwnd, outcome: { success: hwnd !== 0, error, visible: hwnd !== 0 &&
                (guest.getWindowLongW(hwnd, -16) & 0x10000000) !== 0, live: windows.hasWindow(handle),
                window, client, clientOrigin, messages } };
        }
        return { create, windows };
    }

    const desktop = await launch(metrics);
    const handles = requests.map(desktop.create);
    const first = handles[0].outcome.window, next = handles[2].outcome.window;
    const title = desktop.windows.getWindowTitle(handles[0].hwnd);
    const cascadeMoved = first[0] !== next[0] && first[1] !== next[1];
    const stock = await launch();
    const isolated = !stock.windows.hasWindow(handles[0].hwnd);
    const defaultOutcome = stock.create(requests[0]).outcome;
    const wrongStyle = stock.create({ ...requests[0], ExStyle: 0x80000000 }).outcome;
    return { outcomes: handles.map(item => item.outcome), title, cascadeMoved, isolated,
        defaultClient: defaultOutcome.client, wrongStyle: { success: wrongStyle.success, error: wrongStyle.error } };
}

function normalize(outcome, request) {
    if (request.X !== -0x80000000 || !outcome.success) return outcome;
    const [x, y] = outcome.window;
    assert.ok(Number.isInteger(x) && x !== -0x80000000 && Number.isInteger(y) && y !== -0x80000000);
    const offset = (value, index) => value - (index % 2 === 0 ? x : y);
    return { ...outcome, window: outcome.window.map(offset), clientOrigin: outcome.clientOrigin.map(offset),
        messages: outcome.messages.map(item => {
            const { message, data, adjusted } = item;
            if (message === 0x83) return { ...item, data: data.map(offset), adjusted: adjusted.map(offset) };
            if (message === 0x81 || message === 1) {
                const copy = data.slice();
                copy[6] -= y;
                copy[7] -= x;
                return { ...item, data: copy };
            }
            return item;
        }) };
}

const node = await exercise(instantiateManagedModule, createWindowState, bytes, oracle);
for (const [index, request] of oracle.requests.entries())
    assert.deepEqual(normalize(node.outcomes[index], request), normalize(oracle.outcomes[index], request), request.Name);
assert.equal(node.title, "Overlapped creation");
assert.equal(node.cascadeMoved, true);
assert.equal(node.isolated, true);
assert.deepEqual(node.defaultClient, [0, 0, 360, 144]);
assert.deepEqual(node.wrongStyle, { success: false, error: 50 });
const server = await serveLaunch(0, new Uint8Array());
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, bytes, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, bytes, oracle);
    }, { source: exercise.toString(), bytes: [...bytes], oracle });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, edge, nodeVersion: process.version, edgeVersion: browser.version()
    }, null, 2) + "\n");
    console.log("Four Windows x86 creation scenarios matched in Node and Edge.");
} finally {
    await browser.close();
    await server.close();
}
