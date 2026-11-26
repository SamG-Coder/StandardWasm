// Node side of candidate J. Requires `node --experimental-wasm-jspi` (Node 24 does not enable JSPI by default).
import { readFileSync } from "node:fs";
import { runJspi } from "./host-core.mjs";

const [wasm, timerMs, echoCount] = process.argv.slice(2);
// Run with --expose-gc so the host can collect while the Wasm frame is suspended.
const result = await runJspi(readFileSync(wasm), { timerMs: +timerMs, echoCount: +echoCount, gc: globalThis.gc });
process.stdout.write(JSON.stringify(result));
