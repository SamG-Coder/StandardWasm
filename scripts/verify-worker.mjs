// A worker thread that runs verify-jobs.mjs jobs against the shared oracle.
// The main thread passes the case JSON path and the blob bytes in a SharedArrayBuffer,
// so every worker sees one copy of the assembly images.
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { parentPort, workerData } from "node:worker_threads";
import { oracleCases } from "./oracle.mjs";
import { splitBlobs } from "./oracle-store.mjs";
import { runJob } from "./verify-jobs.mjs";

const { casesPath, blobs, blobLengths, modules } = workerData;
const oracle = JSON.parse(readFileSync(casesPath));
const cases = oracleCases({ cases: oracle.cases, blobs: splitBlobs(new Uint8Array(blobs), blobLengths) });
const load = name => readFileSync(join(modules, name + ".wasm"));

parentPort.on("message", async ({ id, job }) => {
    const started = performance.now();
    try {
        const result = await runJob(job, load, cases);
        parentPort.postMessage({ id, result, seconds: (performance.now() - started) / 1000 });
    } catch (error) {
        // Error objects survive structured cloning with their message and stack.
        parentPort.postMessage({ id, error, seconds: (performance.now() - started) / 1000 });
    }
});
