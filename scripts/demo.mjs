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
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated,
            ([module, method, offset]) => {
                const il = offset < 0 ? "" : " IL_" + offset.toString(16).padStart(4, "0");
                console.log("Diagnostic: assembly " + module + ", MethodDef row " + method + il);
            })
        : invoke(instance.exports, demo.method, demoInputs(demo)));
assert.equal(result, decode(demo.expected));
const displayedInputs = demo.inputs.map(input => {
    if (input?.file === "bundle") return demo.assembly + ".dll and dependencies";
    if (input?.file === "self") return demo.assembly + ".dll";
    return input?.name ?? decode(input);
});
const display = result === null && demo.generated?.exceptionType ? demo.generated.exceptionType : result;
console.log(demo.stage + ": " + demo.method + "(" + displayedInputs.join(", ") + ") = " + display);
