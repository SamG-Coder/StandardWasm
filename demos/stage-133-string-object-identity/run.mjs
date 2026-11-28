import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { verifyLinkedDemo } from "../../scripts/linked-demo.mjs";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos", "stage-133-string-object-identity");
const artifact = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const evidenceRoot = join(root, "artifacts", "string-object-identity");
mkdirSync(evidenceRoot, { recursive: true });
const evidence = mkdtempSync(join(evidenceRoot, "run-"));
run("dotnet", ["build", join(stage, "Fixture"), "-c", "Release"]);
const built = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
const path = "StringIdentityFixture.dll", dependency = "StringIdentityDependency.dll";
const oraclePath = join(evidence, "oracle.json");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"),
    [join(built, path), join(built, dependency), oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const file = (directory, name) => ({ path: name, bytes: readFileSync(join(directory, name)) });
// The fixture's netstandard facade forwards System.String through
// System.Runtime. Link the real CoreLib identity used by the x86 oracle.
const framework = ["netstandard.dll", "System.Runtime.dll", "System.Private.CoreLib.dll"]
    .map(name => file(runtime.directory, name));

function exercise(exports, entries) {
    const values = entries.map(({ name }) => exports[name]());
    const repeated = entries.map(({ name }) => exports[name]());
    return {
        identities: values.map(left => repeated.map(right => left === right)),
        nulls: values.map(value => value === null)
    };
}

const result = await verifyLinkedDemo(readFileSync(artifact), file(built, path),
    [file(built, dependency), ...framework], oracle.entries, exercise, oracle.entries);
assert.deepEqual(result.observed, { identities: oracle.identities, nulls: oracle.nulls });
writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2));
console.log("Node and Edge matched " + oracle.entries.length ** 2 + " CLR string reference comparisons.");
