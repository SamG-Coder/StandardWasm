import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { compile, run } from "./toolchain.mjs";
import { decode, invoke, invokeGenerated } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";

const demos = listDemos();
const demo = demos.find(d => d.stage === process.argv[2]);
if (!demo) {
    console.log("Usage: npm run demo -- <stage>\n" + demos.map(d => d.stage).join("\n"));
    process.exit(process.argv[2] ? 1 : 0);
}
if (demo.runner) {
    run(process.execPath, [demo.runner]);
    process.exit(0);
}

run("dotnet", ["build", "-c", "Release", demo.project]);
compile(demo.dll, demo.exportType, demo.wasm);
const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
const result = (demo.generated
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
        : invoke(instance.exports, demo.method, demoInputs(demo)));
assert.equal(result, decode(demo.expected));
console.log(demo.stage + ": " + demo.method + "(" + demo.inputs.map(input => input?.file === "bundle" ? demo.assembly + ".dll and dependencies" : input?.file === "self" ? demo.assembly + ".dll" : input?.name ?? decode(input)).join(", ") + ") = " + result);
