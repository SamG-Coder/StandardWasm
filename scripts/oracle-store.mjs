import { existsSync, readFileSync } from "node:fs";
import { oracleCases } from "./oracle.mjs";

// The oracle is a JSON case list plus the shared assembly images. Verify stores those images
// as one binary file (blobPath) indexed by `blobLengths`, so neither Node nor the browser parses
// tens of megabytes of JSON number arrays. Older worktrees inline `blobs` in the JSON.
export const blobPath = jsonPath => jsonPath.replace(/\.json$/, "") + ".blobs.bin";

export function splitBlobs(bytes, lengths) {
    let offset = 0;
    const blobs = lengths.map(length => bytes.subarray(offset, offset += length));
    if (offset !== bytes.length) throw Error(`Oracle blob file holds ${bytes.length} bytes, index expects ${offset}`);
    return blobs;
}

// Returns { cases, blobs } with each blob as a Uint8Array view of the shared file.
export function readOracle(jsonPath) {
    const oracle = JSON.parse(readFileSync(jsonPath));
    if (Array.isArray(oracle)) return { cases: oracle, blobs: [] };
    if (oracle.blobs) return { cases: oracle.cases, blobs: oracle.blobs.map(blob => Uint8Array.from(blob)) };
    if (!existsSync(blobPath(jsonPath))) throw Error("Oracle blob file is missing: " + blobPath(jsonPath));
    return { cases: oracle.cases, blobs: splitBlobs(readFileSync(blobPath(jsonPath)), oracle.blobLengths) };
}

// Resolves blob indices into the { bytes: number[] } inputs that invoke.mjs transports.
export function loadOracle(jsonPath) {
    const { cases, blobs } = readOracle(jsonPath);
    return { cases: oracleCases({ cases, blobs: blobs.map(blob => Array.from(blob)) }), blobs };
}
