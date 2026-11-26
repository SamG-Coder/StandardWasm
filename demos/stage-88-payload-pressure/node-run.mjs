// Node side. Requires --experimental-wasm-jspi (Node 24 does not enable JSPI by default). gc is not exposed.
import { readFileSync } from "node:fs";
import { measurePressure } from "./pressure-core.mjs";

process.stdout.write(JSON.stringify(await measurePressure(readFileSync(process.argv[2]))));
