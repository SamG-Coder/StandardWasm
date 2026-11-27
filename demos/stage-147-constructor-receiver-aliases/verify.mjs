import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { verifyManagedBundle } from "../../scripts/verify-managed-bundle.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos", "stage-147-constructor-receiver-aliases");
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const output = join(root, "artifacts", "constructor-receiver-aliases");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));
const runtime = wasm32Runtime();
const oraclePath = join(evidence, "oracle.json");
const fixturePath = join(stage, "bin", "Release", "net10.0-windows", "Fixture.dll");
runOn32BitClr(runtime, stage, fixturePath, [oraclePath]);
const { architecture, results: oracle } = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(architecture, "X86");
const fixture = readFileSync(fixturePath);
const desktop = join(runtime.directory, "..", "..", "Microsoft.WindowsDesktop.App", runtime.version);
const framework = [
    ...["System.Windows.Forms.dll", "System.Windows.Forms.Primitives.dll"].map(path => ({ path, bytes: readFileSync(join(desktop, path)) })),
    ...["System.Runtime.dll", "System.Private.CoreLib.dll"].map(path => ({ path, bytes: readFileSync(join(runtime.directory, path)) })),
];
const [bundle] = packEntry({ path: "Fixture.dll", bytes: fixture }, framework, oracle[0].row, true);
const report = await verifyManagedBundle(compiler, bundle, oracle);
writeFileSync(join(evidence, "results.json"), JSON.stringify({ ...report, clr: runtime.version }, null, 2));
console.log(`Node and Edge matched ${report.comparisons} constructor receiver cases from x86 CLR ${runtime.version}.`);
