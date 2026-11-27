import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import binaryen from "binaryen";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-189-window-creation");
const evidence = join(root, "artifacts/stage-189-window-creation");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, createWindows, bytes, metrics) {
    const module = new WebAssembly.Module(Uint8Array.from(bytes));
    const windows = createWindows();
    const popup = 0x80000000;
    const ncCreate = 0x81, ncCalcSize = 0x83, create = 1, minMaxInfo = 0x24;
    let first, next, guest, namePointer, captionPointer, atom, active;
    let nestedSeparated = false, messages, replaced, releaseLibrary;
    const instance = await instantiate(module, {}, [], {}, "Fixture.exe", windows, undefined, {
        windowCreationMetrics: metrics,
        configureNativeLibraries({ registerLibrary, loadLibrary, getProcAddress, freeLibrary }) {
            const signature = "stdcall:i32(p32,u32,u32,i32)";
            registerLibrary("stage189-user32", new Map([
                ["First", { signature, invoke: (hwnd, message, wparam, lparam) => dispatch(1, hwnd, message, wparam, lparam) }],
                ["Next", { signature, invoke: (hwnd, message, wparam, lparam) => dispatch(2, hwnd, message, wparam, lparam) }]
            ]));
            const library = loadLibrary("stage189-user32");
            first = getProcAddress(library, "First");
            next = getProcAddress(library, "Next");
            releaseLibrary = () => freeLibrary(library);
        }
    });
    guest = instance.exports;
    const pageBytes = 65536;
    const base = guest.memory.grow(1) * pageBytes;
    namePointer = base + 64;
    captionPointer = base + 164;
    writeText(namePointer, "StandardWasm.Stage189.É");
    writeText(captionPointer, "É漢");
    const missingName = base + 264;
    writeText(missingName, "Stage189 missing");
    const registration = [0, first, 0, 8, 1, 0, 0, 0, 0, namePointer];
    registration.forEach((field, index) => writeInt(base + index * 4, field));
    atom = guest.registerClassW(base);
    if (!atom) throw Error("Browser registration failed");

    function readInts(address, count) {
        const view = new DataView(guest.memory.buffer);
        return Array.from({ length: count }, (_, index) => view.getInt32((address >>> 0) + index * 4, true));
    }
    function writeInt(address, value) {
        new DataView(guest.memory.buffer).setInt32(address >>> 0, value, true);
    }
    function writeText(address, value) {
        const view = new DataView(guest.memory.buffer);
        for (let index = 0; index < value.length; index++) view.setUint16(address + index * 2, value.charCodeAt(index), true);
        view.setUint16(address + value.length * 2, 0, true);
    }
    function readText(address) {
        if (address === 0) return null;
        const view = new DataView(guest.memory.buffer);
        let text = "";
        for (let at = address >>> 0; at < view.byteLength; at += 2) {
            const unit = view.getUint16(at, true);
            if (unit === 0) return text;
            text += String.fromCharCode(unit);
        }
        throw Error("Invalid host UTF-16 pointer");
    }
    function runWindow(request) {
        const previous = active;
        active = request;
        try {
            const hwnd = guest.createWindowExW(0, request.atom ? atom : namePointer, request.caption === false ? 0 : captionPointer,
                request.style, request.x, request.y, request.width, request.height, 0, 0, 1, request.param);
            return [hwnd, guest.getLastSystemError()];
        } finally { active = previous; }
    }
    function runCase(request) {
        messages = [];
        replaced = true;
        guest.setLastSystemError(0);
        const [hwnd, error] = runWindow(request);
        return { key: request.key, success: hwnd !== 0, error, live: windows.hasWindow(request.hwnd),
            handleMatches: hwnd === 0 || hwnd === request.hwnd, replaced, messages };
    }
    function dispatch(target, hwnd, message, wparam, lparam) {
        const request = active;
        request.hwnd ??= hwnd;
        let data = [], caption = null, className = null;
        if (message === minMaxInfo) {
            data = readInts(lparam, 10);
            if (request.resize) { writeInt(lparam + 24, 72); writeInt(lparam + 28, 60); }
        } else if (message === ncCreate || message === create) {
            if (message === ncCreate) request.creation = lparam;
            const fields = readInts(lparam, 12);
            caption = readText(fields[9]);
            className = (fields[10] >>> 0) <= 0xffff ? "atom" : readText(fields[10]);
            data = [fields[0], fields[1] === 1 ? 1 : 0, ...fields.slice(2, 9), fields[11], lparam === request.creation ? 1 : 0];
            if (message === ncCreate) writeInt(lparam, -9876);
        } else if (message === ncCalcSize) {
            data = readInts(lparam, 4);
            if (request.resize && request.style === popup) {
                writeInt(lparam, data[0] + 3);
                writeInt(lparam + 8, data[2] - 5);
            }
        } else if (message === 3 || message === 5) data = [lparam];
        messages.push({ key: request.key, target, message, wparam, data, caption, className, live: windows.hasWindow(hwnd),
            sameHandle: request.hwnd === hwnd, style: guest.getWindowLongW(hwnd, -16) >>> 0,
            exStyle: guest.getWindowLongW(hwnd, -20) >>> 0 });
        if (target === 1 && guest.setWindowLongW(hwnd, -4, next) !== first) replaced = false;
        if (message === ncCreate && request.nest) {
            const inner = { key: "nested-inner", style: popup, atom: true, x: 20, y: 21, width: 42, height: 43, param: 0x2222 };
            const [nested] = runWindow(inner);
            nestedSeparated = nested !== 0 && inner.creation !== request.creation && readInts(lparam, 1)[0] === -9876;
        }
        if (message === ncCalcSize && request.trap) throw Error("WNDPROC callback stopped");
        if (message === request.abort) return message === create ? -1 : 0;
        return message === ncCreate ? 1 : 0;
    }

    const ordinary = { style: popup, x: 13, y: 17, width: 161, height: 103, param: 0 };
    const cases = [
        runCase({ ...ordinary, key: "overlapped", style: 0, width: 24, height: 25, param: 0x1234, resize: true }),
        runCase({ ...ordinary, key: "popup", atom: true, x: -13, param: -2, resize: true }),
        runCase({ ...ordinary, key: "reject-nccreate", abort: ncCreate }),
        runCase({ ...ordinary, key: "reject-create", abort: create }),
        runCase({ ...ordinary, key: "nested-outer", param: 0x1111, nest: true }),
        runCase({ ...ordinary, key: "null-caption", caption: false })
    ];
    const outcomes = { cases, nestedSeparated };

    const failed = { ...ordinary, key: "throwing", trap: true };
    let exceptionCleanup = false;
    try { runCase(failed); }
    catch (error) { exceptionCleanup = error.message === "WNDPROC callback stopped" && !windows.hasWindow(failed.hwnd); }
    const allocatedPages = guest.memory.buffer.byteLength;
    const retry = runCase({ ...ordinary, key: "retry" }).success;
    const scratchReused = retry && guest.memory.buffer.byteLength === allocatedPages;
    function failure(className, caption, style = popup, parent = 0, width = 5) {
        guest.setLastSystemError(0);
        const hwnd = guest.createWindowExW(0, className, caption, style, 0, 0, width, 5, parent, 0, 1, 0);
        return [hwnd, guest.getLastSystemError()];
    }
    guest.setLastPInvokeError(612);
    const unknownClass = failure(missingName, 0);
    const invalidClass = failure(-1, 0);
    const invalidCaption = failure(atom, -1);
    const tail = guest.memory.buffer.byteLength - 2;
    // Append independent guest memory; a valid string must end within it.
    guest.memory.grow(1);
    const last = tail + pageBytes;
    new DataView(guest.memory.buffer).setUint16(last, 0x0063, true);
    const unterminatedCaption = failure(atom, last);
    const visible = failure(atom, 0, popup | 0x10000000);
    const messageOnly = failure(atom, 0, 0, -3);
    const invalidWidth = failure(atom, 0, popup, 0, -1);
    releaseLibrary();
    const staleProcedure = failure(atom, 0);
    return { outcomes, safeguards: { exceptionCleanup, scratchReused, unknownClass, invalidClass, invalidCaption,
        unterminatedCaption, visible, messageOnly, invalidWidth, staleProcedure, savedError: guest.getLastPInvokeError() } };
}

const node = await exercise(instantiateManagedModule, createWindowState, bytes, oracle.metrics);
assert.deepEqual(node.outcomes, oracle.outcomes);
assert.deepEqual(node.safeguards, { exceptionCleanup: true, scratchReused: true, unknownClass: oracle.unknownClass,
    invalidClass: [0, 998], invalidCaption: [0, 998], unterminatedCaption: [0, 998], visible: [0, 50],
    messageOnly: [0, 50], invalidWidth: [0, 87], staleProcedure: [0, 87], savedError: 612 });
const server = await serveLaunch(0, new Uint8Array());
const edge = await chromium.launch({ channel: "msedge" });
try {
    const page = await edge.newPage();
    await page.goto(server.url);
    const browser = await page.evaluate(async ({ source, bytes, metrics }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, createWindowState, bytes, metrics);
    }, { source: exercise.toString(), bytes: [...bytes], metrics: oracle.metrics });
    assert.deepEqual(browser, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, browser, nodeVersion: process.version, edgeVersion: edge.version()
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 synchronous window creation, nested callbacks and rejection.");
} finally {
    await edge.close();
    await server.close();
}
