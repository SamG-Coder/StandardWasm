import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { verifyManagedBundle } from "../../scripts/verify-managed-bundle.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { verifyWinFormsStartup } from "./verify-winforms.mjs";

const stage = join(root, "demos", "stage-148-executing-assembly");
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
const output = join(root, "artifacts", "executing-assembly");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));
const runtime = wasm32Runtime();
const oraclePath = join(evidence, "oracle.json");
const binaries = join(stage, "Oracle", "bin", "Release", "net10.0");
runOn32BitClr(runtime, join(stage, "Oracle"), join(binaries, "Oracle.dll"), [oraclePath]);
const { architecture, results: oracle } = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(architecture, "X86");
const image = name => ({ path: name, bytes: readFileSync(join(binaries, name)) });
const [bundle] = packEntry(image("ExecutingFixture.dll"), [image("ExecutingDependency.dll")], oracle[0].row, true);
const report = await verifyManagedBundle(compiler, bundle, oracle);
writeFileSync(join(evidence, "results.json"), JSON.stringify({ ...report, clr: runtime.version }, null, 2));
console.log(`Node and Edge matched ${report.comparisons} executing assembly identities from x86 CLR ${runtime.version}.`);

const reference = process.env.STANDARDWASM_WINFORMS_REFERENCE;
if (reference) {
    const startup = await verifyWinFormsStartup(reference, runtime, compilerPath);
    writeFileSync(join(evidence, "winforms-startup.json"), JSON.stringify(startup, null, 2) + "\n");
    console.log("Both original WinForms publish formats reached String.TrimEnd in Node and Edge.");
}
