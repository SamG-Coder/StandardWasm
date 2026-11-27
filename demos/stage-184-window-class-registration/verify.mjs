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

const stage = join(root, "demos/stage-184-window-class-registration");
const evidence = join(root, "artifacts/stage-184-window-class-registration");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, createWindows, bytes) {
    const wasm = new WebAssembly.Module(Uint8Array.from(bytes));
    const messages = [];
    const windows = createWindows();
    const procSignature = "stdcall:i32(p32,u32,u32,i32)";
    const structBytes = 40;
    let original, replacement, wrongType, hwnd;
    const result = instantiate(wasm, {}, [], {}, "Fixture.exe", windows, undefined, {
        configureNativeLibraries({ registerLibrary, loadLibrary, getProcAddress }) {
            function callback(label) {
                return { signature: procSignature, invoke(window, message, wparam, lparam) {
                    if (window !== hwnd) throw Error("Unexpected HWND");
                    if (message === 0x001f || message === 0x000a) messages.push([label, message, wparam, lparam]);
                    if (message === 0x8433) return label === 1 ? wparam + lparam : wparam - lparam;
                    return 0;
                } };
            }
            registerLibrary("fixture-user32", new Map([
                ["Original", callback(1)], ["Replacement", callback(2)],
                ["WrongType", { signature: "stdcall:i32(p32)", invoke() { throw Error("Wrong callback type"); } }]
            ]));
            const library = loadLibrary("fixture-user32");
            original = getProcAddress(library, "Original");
            replacement = getProcAddress(library, "Replacement");
            wrongType = getProcAddress(library, "WrongType");
        }
    });
    const { exports: guest } = await result;
    hwnd = windows.register(0, undefined, { procedure: original });
    const base = guest.memory.grow(1) * 65536;
    const definition = base, output = base + 48, originalName = base + 128, queryName = base + 256;
    const unknownName = base + 384, unicodeCaseName = base + 512;
    const view = new DataView(guest.memory.buffer);
    function text(address, value) {
        for (let index = 0; index < value.length; index++)
            view.setUint16(address + 2 * index, value.charCodeAt(index), true);
        view.setUint16(address + 2 * value.length, 0, true);
    }
    text(originalName, "StandardWasm.Stage184.É");
    text(queryName, "standardwasm.stage184.É");
    text(unknownName, "StandardWasm.Stage184.Missing");
    text(unicodeCaseName, "StandardWasm.Stage184.é");

    function writeDefinition(procedure, module, className, style = 3, menu = 7) {
        const fields = [style, procedure, 4, 8, module, 0, 0, 0, menu, className];
        fields.forEach((value, index) => view.setInt32(definition + 4 * index, value, true));
    }
    function clearThen(action, ...args) {
        guest.setLastSystemError(0);
        const value = action(...args);
        return [value, guest.getLastSystemError()];
    }
    function fields(atom) {
        const data = Array.from({ length: 10 }, (_, index) => view.getInt32(output + 4 * index, true));
        return {
            style: data[0], original: data[1] === original, classExtra: data[2], windowExtra: data[3],
            module: data[4] === 1, icon: data[5], cursor: data[6], background: data[7], menu: data[8],
            name: data[9] === queryName ? "query name" : data[9] === atom ? "query atom" : "unexpected"
        };
    }
    writeDefinition(original, 1, originalName);
    const [atom, registerError] = clearThen(guest.registerClassW, definition);
    const [exactDuplicate, exactDuplicateError] = clearThen(guest.registerClassW, definition);
    writeDefinition(original, 1, queryName);
    const [duplicate, duplicateError] = clearThen(guest.registerClassW, definition);
    // Overwrite the input bytes before reading back the independently copied definition.
    writeDefinition(replacement, 0, unknownName, 1, 9);
    text(originalName, "Overwritten registration buffer");
    const [named, nameError] = clearThen(guest.getClassInfoW, 1, queryName, output);
    const namedInfo = fields(atom);
    const [byAtom, atomError] = clearThen(guest.getClassInfoW, 1, atom, output);
    const atomInfo = fields(atom);
    const [missing, missingError] = clearThen(guest.getClassInfoW, 1, unknownName, output);
    const [otherModule, otherModuleError] = clearThen(guest.getClassInfoW, 0, atom, output);
    const [unicodeCase, unicodeCaseError] = clearThen(guest.getClassInfoW, 1, unicodeCaseName, output);
    const differentModule = 0x01234567;
    writeDefinition(replacement, differentModule, queryName);
    const [differentModuleRegistration, differentModuleError] = clearThen(guest.registerClassW, definition);
    const [namedOther] = clearThen(guest.getClassInfoW, differentModule, queryName, output);
    const differentProcedure = view.getInt32(output + 4, true) === replacement;
    const [atomOther] = clearThen(guest.getClassInfoW, differentModule, atom, output);

    const initial = guest.getWindowLongW(hwnd, -4);
    const [previous, changeError] = clearThen(guest.setWindowLongW, hwnd, -4, replacement);
    const current = guest.getWindowLongW(hwnd, -4);
    const oldResult = guest.callWindowProcW(previous, hwnd, 0x8433, 30, -12);
    const newResult = guest.callWindowProcW(current, hwnd, 0x8433, 30, -12);
    const [badIndex, badIndexError] = clearThen(guest.setWindowLongW, hwnd, -1000, replacement);
    const [badWindow, badWindowError] = clearThen(guest.setWindowLongW, 0, -4, replacement);
    guest.enableWindow(hwnd, 0);
    guest.enableWindow(hwnd, 1);
    const outcomes = {
        structBytes, registered: atom !== 0, registerError, exactDuplicate, exactDuplicateError, duplicate, duplicateError,
        namedEqualsAtom: named === atom, nameError, atomEqualsAtom: byAtom === atom, atomError,
        missing, missingError, otherModule, otherModuleError, unicodeCase, unicodeCaseError,
        differentModuleRegistered: differentModuleRegistration !== 0, differentModuleError,
        sameAtomAcrossModule: atom === differentModuleRegistration,
        differentModuleByName: namedOther === differentModuleRegistration, differentModuleByAtom: atomOther === differentModuleRegistration,
        namedInfo, atomInfo, initialMatches: initial === original, previousMatches: previous === initial,
        currentMatches: current === replacement, changeError, oldResult, newResult, badIndex, badIndexError,
        badWindow, badWindowError, messages: messages.slice()
    };

    guest.setLastPInvokeError(777);
    const end = view.byteLength;
    const truncatedDefinition = clearThen(guest.registerClassW, end - structBytes + 1);
    const nearEndOutput = clearThen(guest.getClassInfoW, 1, atom, end - structBytes + 1);
    const invalidOffset = clearThen(guest.registerClassW, -1);
    writeDefinition(original, 1, end - 2);
    view.setUint16(end - 2, 0x41, true);
    const unterminatedName = clearThen(guest.registerClassW, definition);
    const badLookupName = clearThen(guest.getClassInfoW, 1, -1, output);
    const outputUntouched = view.getInt32(output + 4, true) === replacement;
    writeDefinition(wrongType, 1, unknownName);
    const incompatibleClassCallback = clearThen(guest.registerClassW, definition);
    writeDefinition(original, 1, unknownName, 0x4000);
    const globalClass = clearThen(guest.registerClassW, definition);
    const incompatibleWindowCallback = clearThen(guest.setWindowLongW, hwnd, -4, wrongType);
    const rawWindowCallback = clearThen(guest.setWindowLongW, hwnd, -4, 21);
    const unchangedWindow = guest.getWindowLongW(hwnd, -4) === current;
    const exactEndOutput = clearThen(guest.getClassInfoW, 1, atom, end - structBytes)[0] === atom &&
        view.getInt32(end - 4, true) === atom;

    guest.setLastSystemError(993);
    const lastErrorPreserved = guest.setWindowLongPtrW(hwnd, -4, original) === current &&
        guest.getLastSystemError() === 993;
    const savedError = guest.getLastPInvokeError();
    const zeroPrevious = clearThen(guest.setWindowLongW, windows.register(0), -4, original);
    writeDefinition(original, 1, unknownName);
    guest.setLastSystemError(762);
    const laterAtom = guest.registerClassW(definition);
    const classLastErrorPreserved = laterAtom !== 0 && guest.getLastSystemError() === 762 &&
        guest.getClassInfoW(1, laterAtom, output) === laterAtom && guest.getLastSystemError() === 762;
    const fresh = (await instantiate(wasm, {}, [], {})).exports;
    const freshOutput = fresh.memory.grow(1) * 65536;
    const foreignAtom = [fresh.getClassInfoW(1, atom, freshOutput), fresh.getLastSystemError()];
    return { outcomes, safeguards: { differentProcedure, truncatedDefinition, nearEndOutput, invalidOffset,
        unterminatedName, badLookupName, outputUntouched, incompatibleClassCallback, globalClass, incompatibleWindowCallback,
        rawWindowCallback, unchangedWindow, exactEndOutput, lastErrorPreserved, savedError, zeroPrevious,
        classLastErrorPreserved, foreignAtom } };
}

const node = await exercise(instantiateManagedModule, createWindowState, bytes);
assert.deepEqual(node.outcomes, oracle.outcomes);
assert.deepEqual(node.safeguards, {
    differentProcedure: true, truncatedDefinition: [0, 998], nearEndOutput: [0, 998], invalidOffset: [0, 998],
    unterminatedName: [0, 998], badLookupName: [0, 998], outputUntouched: true, incompatibleClassCallback: [0, 87],
    globalClass: [0, 50], incompatibleWindowCallback: [0, 87], rawWindowCallback: [0, 87], unchangedWindow: true,
    exactEndOutput: true, lastErrorPreserved: true, savedError: 777, zeroPrevious: [0, 0],
    classLastErrorPreserved: true, foreignAtom: [0, 1411]
});
const server = await serveLaunch(0, new Uint8Array());
const edge = await chromium.launch({ channel: "msedge" });
try {
    const page = await edge.newPage();
    await page.goto(server.url);
    const browser = await page.evaluate(async ({ code, input }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        return (0, eval)("(" + code + ")")(instantiateManagedModule, createWindowState, input);
    }, { code: exercise.toString(), input: [...bytes] });
    assert.deepEqual(browser, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, browser, nodeVersion: process.version, edgeVersion: edge.version()
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 class and window procedure oracle.");
} finally {
    await edge.close();
    await server.close();
}
