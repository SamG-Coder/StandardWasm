import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import binaryen from "binaryen";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-194-win32-full-path");
const evidence = join(root, "artifacts/stage-194-win32-full-path");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath, join(evidence, "virtual/base")]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = binaryen.parseText(readFileSync(join(stage, "abi.wat"), "utf8"));
assert.equal(fixture.validate(), 1);
const bytes = fixture.emitBinary();
fixture.dispose();

async function exercise(instantiate, bytes, scenarios) {
    const module = new WebAssembly.Module(Uint8Array.from(bytes));
    const instantiateFor = filesystem => instantiate(module, {}, [], {}, "Virtual.exe", undefined, undefined,
        { win32FileSystem: filesystem });
    const errorSentinel = 0x4321, partSentinel = 0x12345678, bufferSentinel = 0x25c7;
    const source = 65536, target = source + 65536, partSlot = target + 4096;

    function viewFor(guest) {
        guest.memory.grow(2);
        return new DataView(guest.memory.buffer);
    }
    function writeText(view, address, text) {
        for (let index = 0; index < text.length; index++)
            view.setUint16(address + index * 2, text.charCodeAt(index), true);
        view.setUint16(address + text.length * 2, 0, true);
    }
    function query(guest, view, input, capacity, hasFilePart) {
        writeText(view, source, input);
        for (let index = 0; index < capacity + 2; index++) view.setUint16(target + index * 2, bufferSentinel, true);
        view.setUint32(partSlot, partSentinel, true);
        guest.setLastSystemError(errorSentinel);
        const Length = guest.getFullPathNameW(source, capacity, capacity === 0 ? 0 : target, hasFilePart ? partSlot : 0);
        const Error = guest.getLastSystemError();
        const part = view.getUint32(partSlot, true);
        const FilePart = part === 0 ? "null" : part === partSentinel ? "unchanged" :
            part >= target && part < target + capacity * 2 ? "output" : "unexpected";
        const PartOffset = FilePart === "output" ? (part - target) / 2 : -1;
        const success = Length > 0 && Length < capacity;
        const Terminator = success ? view.getUint16(target + Length * 2, true) === 0 :
            capacity > 0 && view.getUint16(target, true) === 0;
        const Text = success ? Array.from({ length: Length }, (_, index) =>
            String.fromCharCode(view.getUint16(target + index * 2, true))).join("") : null;
        const CapacityBoundary = view.getUint16(target + capacity * 2, true) === bufferSentinel &&
            view.getUint16(target + (capacity + 1) * 2, true) === bufferSentinel;
        let TailClear = true;
        if (success) for (let index = Length; index < capacity; index++) TailClear &&= view.getUint16(target + index * 2, true) === 0;
        return { Length, Error, Text, Terminator, FilePart, PartOffset, CapacityBoundary, TailClear };
    }
    const results = [];
    for (const { currentDirectory, cases } of scenarios) {
        const { exports: guest } = await instantiateFor({ currentDirectory });
        const view = viewFor(guest);
        const outcomes = cases.map(({ key, path, capacity, hasFilePart }) => ({
            key, outcome: query(guest, view, path, capacity, hasFilePart)
        }));
        results.push(outcomes);
    }

    const { exports: guest } = await instantiateFor({ currentDirectory: "R:\\virtual\\one" });
    const { exports: fresh } = await instantiateFor({ currentDirectory: "S:\\virtual\\two" });
    const view = viewFor(guest), freshView = viewFor(fresh);
    const perInstance = ["R:\\virtual\\one\\file", "S:\\virtual\\two\\file"].every((path, index) =>
        query(index ? fresh : guest, index ? freshView : view, "file", 64, true).Text === path);
    const { exports: unconfigured } = await instantiateFor(undefined);
    const missingProvider = unconfigured.getFullPathNameW(0, 0, 0, 0);
    const providerError = unconfigured.getLastSystemError();
    let rejectsNoncanonical = false;
    try { await instantiateFor({ currentDirectory: "R:\\virtual\\.." }); }
    catch (error) { rejectsNoncanonical = error instanceof TypeError && /canonical Win32 virtual current directory/u.test(error.message); }
    function unsupported(path) {
        writeText(view, source, path);
        return failure(source, 32, target, partSlot);
    }
    function failure(input, capacity, output, part) {
        view.setUint16(target, bufferSentinel, true);
        guest.setLastSystemError(errorSentinel);
        const status = guest.getFullPathNameW(input, capacity, output, part);
        return [status, guest.getLastSystemError(), view.getUint16(target, true) === bufferSentinel];
    }
    guest.setLastPInvokeError(622);
    writeText(view, source, "file");
    const badInput = failure(-1, 16, target, partSlot);
    const end = view.byteLength;
    const badOutput = failure(source, 8, end - 14, partSlot);
    const badPart = failure(source, 8, target, end - 3);
    const hugeCapacity = failure(source, -1, target, partSlot);
    view.setUint16(end - 2, 0x0041, true);
    const unterminated = failure(end - 2, 32, target, partSlot);
    const aliasedSource = failure(source, 32, source, partSlot);
    const aliasedPart = failure(source, 32, target, target + 2);
    const extended = unsupported("\\\\?\\R:\\folder\\file");
    const device = unsupported("R:\\folder\\NUL");
    const stream = unsupported("R:\\folder\\data:stream");
    const malformedUnc = unsupported("\\\\virtual.invalid");
    const partlyDotted = unsupported("R:\\folder.\\file");
    return { results, safeguards: { perInstance, missingProvider, providerError, rejectsNoncanonical, badInput, badOutput, badPart, hugeCapacity,
        unterminated, aliasedSource, aliasedPart, extended, device, stream, malformedUnc, partlyDotted,
        savedError: guest.getLastPInvokeError() } };
}

const node = await exercise(instantiateManagedModule, bytes, oracle.scenarios);
const expected = oracle.scenarios.map(({ cases }) => cases.map(({ key, outcome }) => ({ key, outcome })));
for (let group = 0; group < expected.length; group++)
    for (let index = 0; index < expected[group].length; index++)
        assert.deepEqual(node.results[group][index], expected[group][index], expected[group][index].key);
assert.deepEqual(node.safeguards, {
    perInstance: true, missingProvider: 0, providerError: 50, rejectsNoncanonical: true, badInput: [0, 998, true], badOutput: [0, 998, true],
    badPart: [0, 998, true], hugeCapacity: [0, 998, true], unterminated: [0, 998, true],
    aliasedSource: [0, 50, true], aliasedPart: [0, 50, true], extended: [0, 50, true], device: [0, 50, true],
    stream: [0, 50, true], malformedUnc: [0, 50, true], partlyDotted: [0, 50, true], savedError: 622
});
const server = await serveLaunch(0, new Uint8Array());
const edge = await chromium.launch({ channel: "msedge" });
try {
    const page = await edge.newPage();
    await page.goto(server.url);
    const browser = await page.evaluate(async ({ code, bytes, scenarios }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + code + ")")(instantiateManagedModule, bytes, scenarios);
    }, { code: exercise.toString(), bytes: [...bytes], scenarios: oracle.scenarios });
    assert.deepEqual(browser, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        oracle, node, browser, nodeVersion: process.version, edgeVersion: edge.version()
    }, null, 2) + "\n");
    console.log("Node and Edge matched " + expected.flat().length + " Windows x86 full-path cases.");
} finally {
    await edge.close();
    await server.close();
}
