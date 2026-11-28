// Times one Wasm-hosted self-compile of the bootstrap compiler, outside npm test.
// Usage: node [--cpu-prof] scripts/profile-bootstrap.mjs [compiler.wasm]
// The default compiler is artifacts/BootstrapLibrary.wasm (B). Pass a generated compiler
// such as artifacts/StandardWasm.Compiler.wasm to time a later generation.
import { readFileSync } from "node:fs";
import { loadOracle } from "./oracle-store.mjs";

const compilerPath = process.argv[2] ?? "artifacts/BootstrapLibrary.wasm";
const started = performance.now();
const fixture = loadOracle("artifacts/oracle.json").cases.find(c => c.bootstrap);
console.log(`oracle ${((performance.now() - started) / 1000).toFixed(1)}s`);

const { instance } = await WebAssembly.instantiate(readFileSync(compilerPath));
const compiler = instance.exports;
function upload(values) {
    const array = compiler.CreateBytes(values.length);
    for (let i = 0; i < values.length; i++) compiler.SetByte(array, i, values[i]);
    return array;
}
const capacity = fixture.capacity ?? (1 << 20);
const t0 = performance.now();
const dll = upload(fixture.inputs[0].bytes), manifest = upload(fixture.inputs[1].bytes);
const output = compiler.CreateBytes(capacity);
const t1 = performance.now();
const length = compiler.CompileModule(dll, manifest, output);
const t2 = performance.now();
const bytes = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
const t3 = performance.now();
const hex = Buffer.from(bytes).toString("hex");
console.log(`upload ${((t1 - t0) / 1000).toFixed(1)}s compile ${((t2 - t1) / 1000).toFixed(1)}s ` +
    `download ${((t3 - t2) / 1000).toFixed(1)}s length ${length} matchesClr ${hex === (process.argv[3] ? readFileSync(process.argv[3]).toString("hex") : fixture.expectedHex)}`);
