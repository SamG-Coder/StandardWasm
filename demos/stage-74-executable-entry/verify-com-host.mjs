import assert from "node:assert/strict";
import { chromium } from "playwright-core";
import { instantiateManagedModule } from "./resource-host.mjs";
import { serveLaunch } from "./launch-server.mjs";

const signature = "stdcall:i32(p32)";
const comModule = "standardwasm:com";
const nativeHelpers = "standardwasm:native";
const i32 = 0x7f, i64 = 0x7e;
const callType = 0;
const encoder = new TextEncoder();
const name = value => [encoder.encode(value).length, ...encoder.encode(value)];
const section = (id, bytes) => {
    const size = [];
    let left = bytes.length;
    do {
        size.push((left & 0x7f) | (left >= 128 ? 0x80 : 0));
        left >>>= 7;
    } while (left);
    return [id, ...size, ...bytes];
};
const type = (parameters, results) => [0x60, parameters.length, ...parameters, results.length, ...results];

function fixture({ mixed = false, memory = true, importName = signature, invalidType = false, duplicate = false } = {}) {
    const types = [
        type([i32, i32], [i32]),
        type([i32], [i32]),
        type([i32, i32, i32], [i32]),
        type([], []),
        type([i32], []),
        type([], [i32]),
        type([i64, i32], [i32])
    ];
    const functions = [[comModule, importName, invalidType ? 6 : callType]];
    if (duplicate) functions.push(functions[0]);
    if (mixed) functions.push(
        [nativeHelpers, "present", 1],
        [nativeHelpers, "library", 1],
        ["native:kernel32.dll", "MulDiv", 2],
        [nativeHelpers, "clearLastError", 3],
        ["native:kernel32.dll", "SetLastError", 4],
        [nativeHelpers, "saveLastError", 3],
        ["internalcall", "System.Runtime.InteropServices.Marshal::GetLastPInvokeError", 5],
        ["native:kernel32.dll", "Absent", 3]
    );
    const imports = functions.map(([owner, entry, kind]) => [...name(owner), ...name(entry), 0, kind]);
    if (memory) imports.unshift([...name("standardwasm:resources"), ...name("memory"), 2, 0, 1]);
    const exports = functions.map(([, entry], index) =>
        [...name(index === 0 ? "raw" : entry.endsWith("::GetLastPInvokeError") ? "readLastError" : entry), 0, index]);
    if (memory) exports.push([...name("memory"), 2, 0]);
    const useVtable = memory && !invalidType;
    if (useVtable) exports.push([...name("callSlot"), 0, functions.length]);
    // i32.load *( *(receiver) + slot * 4 ), then call the COM import with
    // the token and its original receiver. This mirrors the managed wrapper.
    const body = [0, 0x20, 0, 0x28, 2, 0, 0x20, 1, 0x41, 2, 0x74, 0x6a,
        0x28, 2, 0, 0x20, 0, 0x10, 0, 0x0b];
    return Uint8Array.of(
        0, 97, 115, 109, 1, 0, 0, 0,
        ...section(1, [types.length, ...types.flat()]),
        ...section(2, [imports.length, ...imports.flat()]),
        ...(useVtable ? section(3, [1, callType]) : []),
        ...section(7, [exports.length, ...exports.flat()]),
        ...(useVtable ? section(10, [1, body.length, ...body]) : [])
    );
}

async function exercise(instantiate, bytes, dispatchSignature) {
    const denied = async (run, pattern) => {
        try { await run(); }
        catch (error) {
            if (pattern.test(error.message)) return;
            throw error;
        }
        throw Error("Unexpected successful COM dispatch");
    };
    const module = name => new WebAssembly.Module(Uint8Array.from(bytes[name]));
    const slots = (...entries) => new Map(entries.map(([slot, invoke]) => [slot, { signature: dispatchSignature, invoke }]));
    const instantiateProbe = (name, configure) => instantiate(module(name), {}, [], {}, undefined, undefined, configure);
    const invalidToken = /unregistered COM receiver or call token/;
    const invalidVtable = /COM receiver or vtable slot was modified/;
    const calls = [];
    let first, second, sameInterface, badResult;
    const { exports: probe } = await instantiateProbe("mixed", ({ registerInterface }) => {
        first = registerInterface("IExample", slots(
            [1, pointer => {
                calls.push(pointer === first ? "first" : "wrong receiver");
                return 0x80004005;
            }],
            [3, () => { calls.push("other slot"); return 123; }]
        ));
        second = registerInterface("IAnother", slots([1, () => { calls.push("second"); return 72; }]));
        sameInterface = registerInterface("IExample", slots([1, () => { calls.push("same interface"); return 73; }]));
        badResult = registerInterface("IBadResult", slots([1, () => NaN]));
    });

    const words = () => new DataView(probe.memory.buffer);
    const vtable = words().getUint32(first, true);
    const secondVtable = words().getUint32(second, true);
    const token = words().getInt32(vtable + 4, true);
    const secondToken = words().getInt32(secondVtable + 4, true);
    const otherSlot = words().getInt32(vtable + 12, true);
    await denied(() => probe.raw(token, 0), invalidToken);
    await denied(() => probe.raw(0x7fffffff, first), invalidToken);
    await denied(() => probe.raw(token, first + 4), invalidToken);
    await denied(() => probe.raw(token, second), invalidToken);
    await denied(() => probe.raw(token, sameInterface), invalidToken);
    await denied(() => probe.callSlot(first, 0), invalidToken);
    await denied(() => probe.callSlot(badResult, 1), /COM callback must return an i32/);

    words().setInt32(vtable + 4, secondToken, true);
    await denied(() => probe.callSlot(first, 1), invalidToken);
    words().setInt32(vtable + 4, token, true);
    words().setInt32(vtable + 12, token, true);
    await denied(() => probe.callSlot(first, 3), invalidVtable);
    words().setInt32(vtable + 12, otherSlot, true);
    words().setInt32(vtable, token, true);
    await denied(() => probe.callSlot(first, 0), invalidVtable);
    words().setInt32(vtable, 0, true);
    words().setUint32(first, secondVtable, true);
    await denied(() => probe.raw(token, first), invalidVtable);
    words().setUint32(first, vtable, true);

    const ordinalChecks = [0, 3, 8].map(slot => [probe.present(slot), probe.library(slot)]);
    probe.clearLastError();
    probe.SetLastError(1342);
    probe.saveLastError();
    probe.memory.grow(1);
    const result = probe.callSlot(first, 1);
    const preserved = probe.readLastError();
    const vtableDispatch = [probe.callSlot(first, 3), probe.callSlot(second, 1), probe.callSlot(sameInterface, 1)];

    let isolatedPointer;
    const { exports: isolated } = await instantiateProbe("pure", ({ registerInterface }) => {
        isolatedPointer = registerInterface("IExample", slots([1, () => 99]));
    });
    await denied(() => isolated.raw(token, isolatedPointer), invalidToken);
    const { exports: unconfigured } = await instantiateProbe("pure");
    await denied(() => unconfigured.raw(token, first), invalidToken);
    await denied(() => instantiateProbe("wrongName"), /invalid COM dispatch import/);
    await denied(() => instantiateProbe("duplicate"), /invalid COM dispatch import/);
    await denied(() => instantiateProbe("withoutMemory"), /requires the StandardWasm memory import/);
    await denied(() => instantiateProbe("wrongType"), /signature|type/i);
    await denied(() => instantiateProbe("pure", ({ registerInterface }) =>
        registerInterface("IExample", new Map([[1, { signature: "other", invoke: () => 1 }]]))),
    /invalid COM slot registration/);

    return {
        result, preserved, vtableDispatch, ordinalChecks, directNative: probe.MulDiv(10, 3, 2),
        isolated: isolated.callSlot(isolatedPointer, 1), calls
    };
}

const variants = {
    pure: fixture(), mixed: fixture({ mixed: true }), wrongName: fixture({ importName: "unrouted" }),
    withoutMemory: fixture({ memory: false }), wrongType: fixture({ invalidType: true }), duplicate: fixture({ duplicate: true })
};
const bytes = Object.fromEntries(Object.entries(variants).map(([key, module]) => [key, [...module]]));
const node = await exercise(instantiateManagedModule, bytes, signature);
assert.deepEqual(node, {
    result: -2147467259, preserved: 1342, vtableDispatch: [123, 72, 73],
    ordinalChecks: [[0, 0], [1, 1], [0, 1]], directNative: 15, isolated: 99,
    calls: ["first", "other slot", "second", "same interface"]
});
const server = await serveLaunch(0, new Uint8Array());
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, bytes, signature }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + source + ")")(instantiateManagedModule, bytes, signature);
    }, { source: exercise.toString(), bytes, signature });
    assert.deepEqual(edge, node);
    console.log(JSON.stringify({ node: process.version, edge: browser.version(), result: node }));
} finally {
    await browser.close();
    await server.close();
}
