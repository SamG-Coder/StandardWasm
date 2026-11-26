// Stage 79: blocking message loops and modal dialogs on the browser event loop (#176).
// 1. Real WinForms on Windows records the dispatch order around ShowDialog (Oracle/).
// 2. app.wat runs the same dispatch logic as Wasm. Candidate J (JSPI) runs in Node and Edge, and candidate W
//    (worker + Atomics.wait) in cross-origin-isolated Edge. Every trace must equal the Windows trace.
// 3. Records whether the host keeps running during the modal loop and the per-message round-trip latency.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

if (process.platform !== "win32") throw Error("The WinForms oracle requires Windows.");
const stage = join(root, "demos/stage-79-modal-message-loop");
const out = join(root, "artifacts", "modal-message-loop");
mkdirSync(out, { recursive: true });
const timerMs = 100, echoCount = 2000; // 100 ms keeps the dialog open long enough to count frames

// Windows oracle, three runs. The order must be stable before it can serve as the reference.
const oracleRuns = [1, 2, 3].map(i => {
    const file = join(out, `oracle-${i}.json`);
    run("dotnet", ["run", "-c", "Release", "--project", "demos/stage-79-modal-message-loop/Oracle", "--", file]);
    return JSON.parse(readFileSync(file, "utf8"));
});
oracleRuns.forEach(r => assert.deepEqual(r, oracleRuns[0], "WinForms dispatch order varied between runs"));
const oracle = oracleRuns[0];

const wasm = join(out, "app.wasm");
run(process.execPath, ["node_modules/binaryen/bin/wasm-as", join(stage, "app.wat"), "--enable-gc",
    "--enable-reference-types", "-o", wasm]);

const nodeRun = spawnSync(process.execPath, ["--experimental-wasm-jspi", "--expose-gc", join(stage, "node-j.mjs"), wasm, timerMs, echoCount],
    { encoding: "utf8" });
if (nodeRun.status !== 0) throw Error(nodeRun.stderr);
const nodeJ = JSON.parse(nodeRun.stdout);

// localhost is a secure context, so COOP/COEP make the page cross-origin isolated (needed for SharedArrayBuffer).
const files = new Map([
    ["/", ["text/html", "<!doctype html><title>Stage 79</title>"]],
    ["/host-core.mjs", ["text/javascript", readFileSync(join(stage, "host-core.mjs"))]],
    ["/worker-w.mjs", ["text/javascript", readFileSync(join(stage, "worker-w.mjs"))]],
    ["/app.wasm", ["application/wasm", readFileSync(wasm)]]
]);
const server = createServer((request, response) => {
    const file = files.get(request.url);
    if (!file) { response.writeHead(404).end(); return; }
    response.writeHead(200, { "Content-Type": file[0], "Cross-Origin-Opener-Policy": "same-origin",
        "Cross-Origin-Embedder-Policy": "require-corp" });
    response.end(file[1]);
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const edgePath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({ executablePath: process.env.BROWSER_PATH || (existsSync(edgePath) ? edgePath : undefined),
    args: ["--js-flags=--expose-gc"] });
let edge, engine;
try {
    engine = browser.version();
    const page = await browser.newPage();
    await page.goto(`http://localhost:${server.address().port}/`);
    edge = await page.evaluate(async ({ timerMs, echoCount }) => {
        const { runJspi, runWorker } = await import("/host-core.mjs");
        const bytes = await (await fetch("/app.wasm")).arrayBuffer();
        return {
            isolated: crossOriginIsolated, jspi: "Suspending" in WebAssembly,
            J: await runJspi(bytes, { timerMs, echoCount, gc: globalThis.gc }),
            W: await runWorker(bytes, { timerMs, echoCount }, "/worker-w.mjs")
        };
    }, { timerMs, echoCount });
} finally {
    await browser.close();
    server.close();
}

const results = { oracle, node: { version: process.version, J: nodeJ }, edge: { version: engine, ...edge } };
writeFileSync(join(out, "results.json"), JSON.stringify(results, null, 2) + "\n");
console.log("Windows WinForms order: " + oracle.join(" > "));
for (const [label, r] of [["Node " + process.version + " J", nodeJ], ["Edge " + engine + " J", edge.J], ["Edge " + engine + " W", edge.W]]) {
    const same = JSON.stringify(r.trace) === JSON.stringify(oracle);
    console.log(`${label}: trace ${same ? "matches Windows" : "DIFFERS: " + r.trace.join(" > ")}; modal ${r.modalMs} ms with ` +
        `${r.hostTasksDuringModal} host tasks${r.framesDuringModal === null ? "" : " and " + r.framesDuringModal + " frames"}; ` +
        `${r.usPerTrip} us per message round trip; GC reference on the suspended frame ` +
        `${r.stateCheck === 1 ? "kept its identity and state" : "LOST"}` +
        (r.collectedWhileSuspended ? ` across ${r.collectedWhileSuspended} forced collection(s) while suspended` : ""));
    assert.deepEqual(r.trace, oracle);
    assert.equal(r.stateCheck, 1);
}
console.log(`Edge page cross-origin isolated: ${edge.isolated}; JSPI available: ${edge.jspi}`);
