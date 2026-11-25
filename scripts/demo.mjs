import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { compile, run } from "./toolchain.mjs";
import { listDemos } from "./demos.mjs";

const demos = listDemos();
const demo = demos.find(d => d.stage === process.argv[2]);
if (!demo) {
    console.log("Usage: npm run demo -- <stage>\n" + demos.map(d => d.stage).join("\n"));
    process.exit(process.argv[2] ? 1 : 0);
}
run("dotnet", ["build", "-c", "Release", demo.project]);
compile(demo.dll, demo.exportType, demo.wasm);
const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
const result = instance.exports[demo.method](...demo.inputs);
assert.equal(result, demo.expected);
console.log(demo.stage + ": " + demo.method + "(" + demo.inputs.join(", ") + ") = " + result);
