// Node side. Requires --experimental-wasm-jspi (Node 24 does not enable JSPI by default). gc is not exposed.
import { readFileSync } from "node:fs";
import { measureModelB, measurePressure } from "./pressure-core.mjs";

process.stdout.write(JSON.stringify({ c: await measurePressure(readFileSync(process.argv[2])),
    b: await measureModelB(readFileSync(process.argv[3])) }));
