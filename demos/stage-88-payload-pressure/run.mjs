// Stage 88: bounding linear-memory growth for model C payloads (#196, decision 0025 follow-up).
// Churns 64 KiB payloads under five strategies and two allocation patterns, in Node and Edge, without forced GC,
// and reports peak linear memory, time, and how often the JSPI slow path had to yield.
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos/stage-88-payload-pressure");
const out = join(root, "artifacts", "payload-pressure");
mkdirSync(out, { recursive: true });
const wasm = join(out, "pressure.wasm");
run(process.execPath, ["node_modules/binaryen/bin/wasm-as", join(stage, "pressure.wat"), "--enable-gc",
    "--enable-reference-types", "--enable-bulk-memory", "-o", wasm]);

const nodeRun = spawnSync(process.execPath, ["--experimental-wasm-jspi", join(stage, "node-run.mjs"), wasm],
    { encoding: "utf8", maxBuffer: 1 << 24 });
if (nodeRun.status !== 0) throw Error(nodeRun.stderr);
const node = JSON.parse(nodeRun.stdout);

const edgePath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({ executablePath: process.env.BROWSER_PATH || (existsSync(edgePath) ? edgePath : undefined) });
let edge, engine;
try {
    engine = browser.version();
    const page = await browser.newPage();
    await page.route("http://payload-pressure.test/**", route => route.fulfill(
        new URL(route.request().url()).pathname === "/pressure-core.mjs"
            ? { contentType: "text/javascript", body: readFileSync(join(stage, "pressure-core.mjs")) }
            : { contentType: "text/html", body: "<title>Payload pressure</title>" }));
    await page.goto("http://payload-pressure.test/");
    edge = await page.evaluate(async bytes =>
        (await import("/pressure-core.mjs")).measurePressure(new Uint8Array(bytes)), Array.from(readFileSync(wasm)));
} finally {
    await browser.close();
}

writeFileSync(join(out, "results.json"), JSON.stringify({ node: { version: process.version, results: node },
    edge: { version: engine, results: edge } }, null, 2) + "\n");
for (const [label, results] of [["Node " + process.version, node], ["Edge " + engine, edge]]) {
    console.log(label);
    for (const r of results)
        console.log(`  ${r.strategy.padEnd(30)} ${r.pattern.padEnd(30)} peak ${String(r.peakMiB).padStart(7)} MiB of ` +
            `${r.allocatedMiB} MiB, ${r.ms} ms, ${r.freed} freed, slow path ${r.relieveCalls} (${r.relieveFailures} gave up)` +
            (r.exhausted ? ", EXHAUSTED" : "") + (r.timedOut ? ", STOPPED AT 60 s" : ""));
}
