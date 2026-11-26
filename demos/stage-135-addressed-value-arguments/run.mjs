import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { verifyLinkedDemo } from "../../scripts/linked-demo.mjs";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-135-addressed-value-arguments");
const artifact = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const evidenceRoot = join(root, "artifacts", "addressed-value-arguments");
mkdirSync(evidenceRoot, { recursive: true });
const evidence = mkdtempSync(join(evidenceRoot, "run-"));
run("dotnet", ["build", join(stage, "Fixture"), "-c", "Release"]);
const built = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
const path = "AddressedValueFixture.dll";
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built, path), oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const file = name => ({ path: name, bytes: readFileSync(join(built, name)) });

function exercise(exports, calls) {
    return calls.map(({ name, inputs }) => exports[name](...inputs));
}

const result = await verifyLinkedDemo(readFileSync(artifact), file(path), [], oracle.entries, exercise, oracle.calls);
assert.deepEqual(result.observed, oracle.expected);
writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2));
console.log("Node and Edge matched " + oracle.calls.length + " CLR addressed value-argument outcomes.");
