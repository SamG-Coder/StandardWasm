import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { verifyManagedBundle } from "../../scripts/verify-managed-bundle.mjs";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";

const stage = join(root, "demos/stage-316-corelib-array-initializers");
const evidence = join(root, "artifacts/stage-316-corelib-array-initializers");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const project = join(stage, "Oracle");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, project, join(project, "bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = readFileSync(oracle.assembly);
const framework = ["netstandard.dll", "System.Runtime.dll", "System.Private.CoreLib.dll"]
    .map(path => ({ path, bytes: readFileSync(join(runtime.directory, path)) }));
const [bundle] = packEntry({ path: "ArrayInitializers.dll", bytes: fixture }, framework, oracle.cases[0].row, false);
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
const result = await verifyManagedBundle(compiler, bundle, oracle.cases);
const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
const report = { compiler: sha256(compiler), assembly: sha256(fixture), coreLib: sha256(framework.at(-1).bytes),
    clr: oracle.clr, ...result };
writeFileSync(join(evidence, "results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ comparisons: report.comparisons, clr: report.clr, node: report.node, edge: report.edge }));
