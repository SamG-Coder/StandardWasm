// Runs the verification jobs on a pool of Node worker threads.
import { readFileSync } from "node:fs";
import { Worker } from "node:worker_threads";
import { blobPath } from "./oracle-store.mjs";

// Starts `size` workers over the oracle at `oraclePath` (cases JSON plus blob file) and returns
// an `execute(job)` function and a `close()` function. Jobs queue until a worker is free.
export function startNodePool(oraclePath, modules, size, onJob) {
    const { blobLengths } = JSON.parse(readFileSync(oraclePath));
    const file = readFileSync(blobPath(oraclePath));
    const blobs = new SharedArrayBuffer(file.length);
    new Uint8Array(blobs).set(file);
    const workerData = { casesPath: oraclePath, blobs, blobLengths, modules };
    const idle = [], queue = [], pending = new Map();
    let next = 0;
    const dispatch = () => {
        while (idle.length && queue.length) {
            const worker = idle.pop();
            const { id, job } = queue.shift();
            worker.postMessage({ id, job });
        }
    };
    const workers = Array.from({ length: size }, () => {
        const worker = new Worker(new URL("./verify-worker.mjs", import.meta.url), {
            workerData, resourceLimits: { maxOldGenerationSizeMb: 4096 }
        });
        worker.on("message", ({ id, result, error, seconds }) => {
            const { job, resolve, reject } = pending.get(id);
            pending.delete(id);
            onJob?.(job, seconds, error);
            idle.push(worker);
            dispatch();
            if (error) reject(error); else resolve(result);
        });
        worker.on("error", error => {
            for (const { reject } of pending.values()) reject(error);
            pending.clear();
        });
        idle.push(worker);
        return worker;
    });
    return {
        execute: job => new Promise((resolve, reject) => {
            const id = next++;
            pending.set(id, { job, resolve, reject });
            queue.push({ id, job });
            dispatch();
        }),
        close: () => Promise.all(workers.map(worker => worker.terminate()))
    };
}
