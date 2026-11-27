import { once } from "node:events";
import { existsSync, readFileSync } from "node:fs";
import { createServer } from "node:http";
import { chromium } from "playwright-core";
import { blobPath } from "./oracle-store.mjs";
import { verifyConcurrently } from "./verify-jobs.mjs";

// Runs the verification jobs in Edge, `lanes` at a time, one browser context per lane so each
// has its own renderer process and main thread (decision 0102). Results are combined exactly as
// in Node. `onJob(job, seconds, error)` reports each finished job.
export async function verifyBrowserSuite(demos, oraclePath, cases, jobs, lanes, onJob) {
    // Browser traffic stays on loopback. Large shared blobs exceed the
    // DevTools pipe's message limit when fulfilled through Playwright routes.
    const assets = new Map([
        ["/", ["text/html", "<!doctype html><title>StandardWasm verification</title>"]],
        ["/oracle.json", ["application/json", readFileSync(oraclePath)]],
        ["/blobs.bin", ["application/octet-stream", readFileSync(blobPath(oraclePath))]],
        ...["oracle", "invoke", "verify-jobs"].map(name =>
            ["/" + name + ".mjs", ["text/javascript", readFileSync(new URL(name + ".mjs", import.meta.url))]]),
        ...demos.map(demo => ["/" + demo.assembly, ["application/wasm", readFileSync(demo.wasm)]])
    ]);
    const server = createServer((request, response) => {
        const asset = assets.get(request.url);
        if (!asset) {
            response.writeHead(404);
            response.end();
            return;
        }
        const [contentType, body] = asset;
        response.writeHead(200, { "Content-Type": contentType, "Content-Length": Buffer.byteLength(body) });
        response.end(body);
    });
    server.listen(0, "127.0.0.1");
    await once(server, "listening");
    try {
        const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
        // Node worker threads default to a 4 MB stack. Give each renderer's V8 the same limit:
        // with every core busy, tier-up lags and the compiler's recursion over a 236-deep type
        // argument (BootstrapLibrary RecursiveTypeArgument) still runs in Liftoff frames, which
        // overflow the default ~1 MB limit (#316, #318). The renderer thread stack is larger.
        const browser = await chromium.launch({
            executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined),
            args: ["--js-flags=--stack-size=3800"]
        });
        try {
            const origin = "http://127.0.0.1:" + server.address().port + "/";
            const pages = await Promise.all(Array.from({ length: lanes }, async () => {
                const page = await (await browser.newContext()).newPage();
                await page.goto(origin);
                await page.evaluate(async () => {
                    const { oracleCases } = await import("/oracle.mjs");
                    const oracle = await (await fetch("/oracle.json")).json();
                    const bytes = new Uint8Array(await (await fetch("/blobs.bin")).arrayBuffer());
                    let offset = 0;
                    // Keep a view over the shared buffer for each distinct payload.
                    oracle.blobs = oracle.blobLengths.map(length => bytes.subarray(offset, offset += length));
                    globalThis.verificationCases = oracleCases(oracle);
                    globalThis.verificationJobs = await import("/verify-jobs.mjs");
                });
                return page;
            }));
            const idle = [...pages], waiting = [];
            const execute = async job => {
                const page = idle.pop() ?? await new Promise(resume => waiting.push(resume));
                const started = performance.now();
                try {
                    const result = await page.evaluate(job => globalThis.verificationJobs.runJob(job,
                        async name => (await fetch("/" + name)).arrayBuffer(), globalThis.verificationCases), job);
                    onJob?.(job, (performance.now() - started) / 1000);
                    return result;
                } catch (error) {
                    onJob?.(job, (performance.now() - started) / 1000, error);
                    throw error;
                } finally {
                    const next = waiting.shift();
                    if (next) next(page); else idle.push(page);
                }
            };
            return { result: await verifyConcurrently(cases, jobs, execute), version: browser.version() };
        } finally {
            await browser.close();
        }
    } finally {
        await new Promise(resolve => server.close(resolve));
    }
}
