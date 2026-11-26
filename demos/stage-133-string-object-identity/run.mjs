import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { verifyLinkedDemo } from "../../scripts/linked-demo.mjs";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-133-string-object-identity");
const artifact = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
const evidenceRoot = join(root, "artifacts", "string-object-identity");
mkdirSync(evidenceRoot, { recursive: true });
const evidence = mkdtempSync(join(evidenceRoot, "run-"));
run("dotnet", ["build", join(stage, "Fixture"), "-c", "Release"]);
const built = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
const path = "StringIdentityFixture.dll", dependency = "StringIdentityDependency.dll";
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "--project", join(stage, "Oracle"), "-c", "Release", "--",
    join(built, path), join(built, dependency), oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const file = name => ({ path: name, bytes: readFileSync(join(built, name)) });

function exercise(exports, entries) {
    const values = entries.map(({ name }) => exports[name]());
    const repeated = entries.map(({ name }) => exports[name]());
    return {
        identities: values.map(left => repeated.map(right => left === right)),
        nulls: values.map(value => value === null)
    };
}

const result = await verifyLinkedDemo(readFileSync(artifact), file(path), [file(dependency)], oracle.entries, exercise, oracle.entries);
assert.deepEqual(result.observed, { identities: oracle.identities, nulls: oracle.nulls });
writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2));
console.log("Node and Edge matched " + oracle.entries.length ** 2 + " CLR string reference comparisons.");
