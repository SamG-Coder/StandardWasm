import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = "stage-351-runtime-type-casts";
const oracleDir = join(root, "demos", stage, "Oracle");
const evidence = join(root, "artifacts", stage);
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(oracleDir, "Oracle.csproj"),
    join(oracleDir, "bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));

function unsigned(value) {
    const bytes = [];
    do {
        const low = value & 127;
        value >>>= 7;
        bytes.push(low | (value ? 128 : 0));
    } while (value);
    return bytes;
}

function section(id, bytes) {
    return [id, ...unsigned(bytes.length), ...bytes];
}

function exportEntry(name, kind, index) {
    const bytes = Buffer.from(name);
    return [...unsigned(bytes.length), ...bytes, kind, ...unsigned(index)];
}

const bodies = ["helper", "test", "cast", "proven"].map(name => {
    const bytes = Buffer.from(oracle[name], "base64");
    return [...unsigned(bytes.length), ...bytes];
});
const globals = oracle.rows.flatMap(row => [
    0x6d, 0,             // Immutable eqref global.
    0x41, 1,             // Module slot.
    0x41, row & 127,     // The oracle uses rows that fit one signed byte.
    0xfb, 0, 0, 0x0b    // struct.new, type 0.
]);
assert.ok(oracle.rows.every(row => row >= -64 && row < 64));
const exports = oracle.rows.flatMap((_, index) => exportEntry("value" + index, 3, index));
const moduleBytes = Uint8Array.from([
    0, 0x61, 0x73, 0x6d, 1, 0, 0, 0,
    ...section(1, [3, 0x5f, 2, 0x7f, 0, 0x7f, 0, 0x60, 1, 0x6d, 1, 0x6d, 0x60, 0, 0]),
    ...section(3, [4, 1, 1, 1, 1]),
    ...section(13, [5, 0, 2, 0, 2, 0, 2, 0, 2, 0, 2]),
    ...section(6, [oracle.rows.length, ...globals]),
    ...section(7, [oracle.rows.length + 4, ...exports,
        ...exportEntry("test", 0, 1), ...exportEntry("cast", 0, 2),
        ...exportEntry("proven", 0, 3), ...exportEntry("invalidCast", 4, 4)]),
    ...section(10, [bodies.length, ...bodies.flat()])
]);
writeFileSync(join(evidence, "casts.wasm"), moduleBytes);

async function verify(bytes, cases) {
    const { instance: { exports: guest } } = await WebAssembly.instantiate(bytes);
    const values = cases.map((_, index) => guest["value" + index]?.value ?? null);
    const outcomes = cases.map(({ name }, index) => {
        const source = values[index];
        const result = guest.test(source);
        if (result !== null && result !== source) throw Error("Cast changed object identity.");
        const preserved = result !== null;
        try {
            return { name, preserved, cast: guest.cast(source) === source ? "identity" : "wrong value" };
        } catch (failure) {
            if (!(failure instanceof WebAssembly.Exception) || !failure.is(guest.invalidCast)) throw failure;
            return { name, preserved, cast: "invalid cast" };
        }
    });
    if (guest.proven(values[0]) !== values[0] || guest.proven(null) !== null)
        throw Error("Proven casts must preserve references and null.");
    return outcomes;
}

const node = await verify(moduleBytes, oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const edge = await page.evaluate(async ({ source, bytes, cases }) =>
        (0, eval)("(" + source + ")")(Uint8Array.from(bytes), cases),
    { source: verify.toString(), bytes: [...moduleBytes], cases: oracle.cases });
    assert.deepEqual(node, oracle.cases);
    assert.deepEqual(edge, oracle.cases);
    const result = { clr: oracle.runtime, node: process.version, edge: browser.version(), cases: node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log(JSON.stringify(result));
} finally {
    await browser.close();
}
