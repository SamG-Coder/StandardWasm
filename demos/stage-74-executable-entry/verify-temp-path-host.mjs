import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { createTempPathHost } from "./temp-path-host.mjs";

const artifactRoot = join(root, "artifacts", "temp-path-host");
mkdirSync(artifactRoot, { recursive: true });
const evidence = mkdtempSync(join(artifactRoot, "run-"));
const oraclePath = join(evidence, "oracle.json");
const project = join(root, "demos", "stage-74-executable-entry", "TempPathOracle");
runOn32BitClr(wasm32Runtime(), project, join(project, "bin", "Release", "net10.0", "TempPathOracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");

function exercise(create, path, capacities) {
    const memory = new WebAssembly.Memory({ initial: 1 });
    const errors = { system: 777, pinvoke: 321 };
    const { getTempPathW, getTempPath2W } = create(memory, path, errors);
    const offset = 31; // UTF-16 writes need not be naturally aligned in Wasm.
    memory.grow(1);
    const observe = query => capacities.map(capacity => {
        const buffer = new DataView(memory.buffer, offset, 2 * (capacity + 2));
        for (let i = 0; i < capacity + 2; i++) buffer.setUint16(2 * i, 33, true);
        errors.system = 777;
        const count = query(capacity, capacity ? offset + 2 : 0);
        const text = Array.from({ length: capacity + 2 }, (_, i) => String.fromCharCode(buffer.getUint16(2 * i, true))).join("");
        return { capacity, count, system: errors.system, pinvoke: errors.pinvoke, text };
    });

    const legacy = observe(getTempPathW);
    const current = observe(getTempPath2W);
    const end = new Uint8Array(memory.buffer);
    end[end.length - 1] = 97;
    const invalid = [getTempPathW(1, 0), getTempPath2W(1, end.length - 1), getTempPathW(-1, 31), errors.system, errors.pinvoke, end[end.length - 1]];
    const secondMemory = new WebAssembly.Memory({ initial: 1 });
    const secondErrors = { system: 0, pinvoke: 987 };
    const { getTempPathW: other } = create(secondMemory, "V:\\Scratch\\", secondErrors);
    const secondPathLength = other(20, 8);
    const secondWords = new Uint16Array(secondMemory.buffer, 8, secondPathLength + 1);
    const secondPath = String.fromCharCode(...secondWords);
    const isolated = [secondPath, secondErrors.system, secondErrors.pinvoke, errors.system, errors.pinvoke];
    const rejectedDirectories = ["relative\\", "C:\\missing-slash", "C:\\other\\..\\", "C:\\\0\\"].map(directory => {
        try { create(memory, directory, errors); }
        catch (error) { return error instanceof TypeError; }
        return false;
    });
    return { legacy, current, invalid, isolated, rejectedDirectories };
}

const capacities = oracle.current.map(({ capacity }) => capacity);
const node = exercise(createTempPathHost, oracle.path, capacities);
assert.deepEqual(node.legacy, oracle.legacy);
assert.deepEqual(node.current, oracle.current);
assert.deepEqual(node.invalid, [0, 0, 0, 998, 321, 97]);
assert.deepEqual(node.isolated, ["V:\\Scratch\\\0", 0, 987, 998, 321]);
assert.deepEqual(node.rejectedDirectories, [true, true, true, true]);

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        if (new URL(route.request().url()).pathname === "/temp-path-host.mjs")
            return route.fulfill({ contentType: "text/javascript", body: readFileSync(new URL("./temp-path-host.mjs", import.meta.url)) });
        return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, path, capacities }) => {
        const { createTempPathHost } = await import("/temp-path-host.mjs");
        return (0, eval)("(" + source + ")")(createTempPathHost, path, capacities);
    }, { source: exercise.toString(), path: oracle.path, capacities });
    assert.deepEqual(edge, node);
    const results = { oracle, node, edge, nodeVersion: process.version, edgeVersion: browser.version() };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2) + "\n");
    console.log("Node and Edge matched " + capacities.length + " buffer cases for both Windows exports, including the system error slot and Unicode output.");
} finally {
    await browser.close();
}
