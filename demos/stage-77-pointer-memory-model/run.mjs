// Stage 77: memory-model experiment for pointers into managed memory (#167).
// 1. Build PointerPatterns.dll and record CLR results for every scenario.
// 2. Assemble the hand-written lowerings of candidates A, B and C (prototypes.mjs).
// 3. Check them against the CLR and time the cost probes in Node and in Edge. Write artifacts/pointer-memory-model.json.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { setFlagsFromString } from "node:v8";
import { runInNewContext } from "node:vm";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { buildWat, modelNames } from "./prototypes.mjs";
import { liveness, measure, pressure } from "./measure-core.mjs";

const stage = "demos/stage-77-pointer-memory-model";
const out = join(root, "artifacts", "pointer-memory-model");
mkdirSync(out, { recursive: true });

run("dotnet", ["run", "-c", "Release", "--project", stage + "/Oracle", "--", join(out, "oracle.json")]);
const cases = JSON.parse(readFileSync(join(out, "oracle.json"), "utf8"));

const models = {};
for (const name of modelNames) {
    const wat = join(out, `model-${name}.wat`), wasm = join(out, `model-${name}.wasm`);
    writeFileSync(wat, buildWat(name));
    run(process.execPath, ["node_modules/binaryen/bin/wasm-as", wat, "--enable-gc", "--enable-reference-types",
        "--enable-bulk-memory", "-o", wasm]);
    models[name] = readFileSync(wasm);
    if (!WebAssembly.validate(models[name])) throw Error(`Node rejected model ${name}`);
}

const probes = {};
for (const name of ["liveness", "pressure"]) {
    const wasm = join(out, name + ".wasm");
    run(process.execPath, ["node_modules/binaryen/bin/wasm-as", join(root, stage, name + ".wat"), "--enable-gc",
        "--enable-reference-types", "--enable-bulk-memory", "-o", wasm]);
    probes[name] = readFileSync(wasm);
}

// The pressure probe runs before gc is exposed, so collection follows the engine's own schedule.
const nodePressure = await pressure(probes.pressure);
setFlagsFromString("--expose-gc");
const gc = runInNewContext("gc");
const node = await measure(models, cases, gc);
const nodeLiveness = await liveness(probes.liveness, gc);

const edgePath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edgePath) ? edgePath : undefined),
    args: ["--js-flags=--expose-gc"]
});
let edge, engine;
try {
    engine = browser.version();
    const page = await browser.newPage();
    await page.route("http://pointer-model.test/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/measure-core.mjs")
            return route.fulfill({ contentType: "text/javascript", body: readFileSync(join(root, stage, "measure-core.mjs")) });
        return route.fulfill({ contentType: "text/html", body: "<title>Pointer memory model</title>" });
    });
    await page.goto("http://pointer-model.test/");
    edge = await page.evaluate(async ({ wasm, cases, live }) => {
        const { liveness, measure } = await import("/measure-core.mjs");
        const models = Object.fromEntries(Object.entries(wasm).map(([k, v]) => [k, new Uint8Array(v)]));
        return { models: await measure(models, cases, globalThis.gc),
            liveness: await liveness(new Uint8Array(live), globalThis.gc) };
    }, { wasm: Object.fromEntries(Object.entries(models).map(([k, v]) => [k, Array.from(v)])), cases,
        live: Array.from(probes.liveness) });
} finally {
    await browser.close();
}
// A second browser without --expose-gc runs the pressure probe, so collection follows the engine's own schedule.
const plain = await chromium.launch({ executablePath: process.env.BROWSER_PATH || (existsSync(edgePath) ? edgePath : undefined) });
try {
    const page = await plain.newPage();
    await page.route("http://pointer-model.test/**", route => route.fulfill(new URL(route.request().url()).pathname === "/measure-core.mjs"
        ? { contentType: "text/javascript", body: readFileSync(join(root, stage, "measure-core.mjs")) }
        : { contentType: "text/html", body: "<title>Pointer memory model</title>" }));
    await page.goto("http://pointer-model.test/");
    edge.pressure = await page.evaluate(async bytes =>
        (await import("/measure-core.mjs")).pressure(new Uint8Array(bytes)), Array.from(probes.pressure));
} finally {
    await plain.close();
}

const results = {
    node: { version: process.version, models: node, liveness: nodeLiveness, pressure: nodePressure },
    edge: { version: engine, ...edge }
};
writeFileSync(join(out, "results.json"), JSON.stringify(results, null, 2) + "\n");
for (const [label, r] of [["Node " + process.version, node], ["Edge " + engine, edge.models]]) {
    console.log(label);
    for (const [name, m] of Object.entries(r)) {
        const reclaim = m.reclaim ? `, reclaim ${m.reclaim.freed}/${m.reclaim.allocated} freed (register ${m.reclaim.registerMs} ms)` : "";
        console.log(`  ${name}: ${m.correct}/${m.cases} CLR cases; access ${m.accessMs} ms; fixed 64 B ${m.fixedSmallMs} ms; ` +
            `fixed 64 KiB ${m.fixedLargeMs} ms; alloc ${m.allocMs} ms${reclaim}`);
        for (const miss of m.mismatches.slice(0, 3))
            console.log(`     mismatch ${miss.scenario}(${miss.input}): CLR ${miss.expected}, model ${miss.actual}`);
    }
}
for (const [label, r] of [["Node", results.node], ["Edge", results.edge]]) {
    for (const l of r.liveness)
        console.log(`${label} liveness, Span ${l.keepOwner ? "carries its owner" : "ownerless"}: span[0] = ${l.spanByte} ` +
            `(array wrote ${l.expected}); ${l.freed} payloads freed and reused meanwhile`);
    for (const p of r.pressure)
        console.log(`${label} pressure, ${p.pattern}: ${p.allocatedMiB} MiB allocated, linear memory peak ${p.peakMiB} MiB, ` +
            `${p.freedWhenDone}/${p.allocations} freed when the loop ended, ${p.freedAfter200ms} after 200 ms`);
}
