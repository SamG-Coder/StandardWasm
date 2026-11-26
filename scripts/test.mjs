import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { decode, invoke, invokeGenerated } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { chromium } from "playwright-core";
import { compile, root, run } from "./toolchain.mjs";
import { verify } from "./verify.mjs";

process.chdir(root);
mkdirSync("artifacts", { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", "Verify", "--", "artifacts/oracle.json"]);
const demos = listDemos();
for (const demo of demos) {
    compile(demo.dll, demo.exportType, demo.wasm);
    const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
    assert.equal((demo.generated
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
        : invoke(instance.exports, demo.method, demoInputs(demo))), decode(demo.expected), demo.stage);
}
const scalarDemo = demos.find(d => d.assembly === "Sample");
const rejected = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", scalarDemo.dll, "Sample.CheckedArithmetic", "artifacts/unsupported.wat"], { encoding: "utf8" });
assert.equal(rejected.status, 1);
assert.match(rejected.stderr, /Increment.*IL_[0-9a-f]+.*add\.ovf/);
// 64-bit checked arithmetic must be diagnosed too, with the method and IL offset.
const wideDemo = demos.find(d => d.assembly === "LongIntegers");
const rejectedWide = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", wideDemo.dll, "LongIntegers.CheckedWide", "artifacts/unsupported-wide.wat"], { encoding: "utf8" });
assert.equal(rejectedWide.status, 1);
assert.match(rejectedWide.stderr, /CheckedWide::Increment\(System\.Int64\) at IL_[0-9a-f]+ \(add\.ovf\)/);
// A checked float-to-int conversion must be diagnosed. Float remainder is lowered exactly since stage 33.
const realsDemo = demos.find(d => d.assembly === "FloatingPoint");
for (const [type, pattern] of [["CheckedConversions", /CheckedConversions::Narrow\(System\.Double\) at IL_[0-9a-f]+ \(conv\.ovf\.i4\)/]]) {
    const rejectedReal = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
        "--", realsDemo.dll, "FloatingPoint." + type, "artifacts/unsupported-real.wat"], { encoding: "utf8" });
    assert.equal(rejectedReal.status, 1);
    assert.match(rejectedReal.stderr, pattern);
}
// A BCL method outside the intrinsic table (decision 0003) must stay an unlinked-call diagnostic.
const mathDemo = demos.find(d => d.assembly === "MathIntrinsics");
const rejectedMath = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", mathDemo.dll, "MathIntrinsics.UnmappedMath", "artifacts/unsupported-math.wat"], { encoding: "utf8" });
assert.equal(rejectedMath.status, 1);
assert.match(rejectedMath.stderr, /UnmappedMath::Power\(System\.Double,System\.Double\) at IL_[0-9a-f]+ \(call\): Unlinked call: System\.Double System\.Math::Pow/);
// A dependency that defines its own System.Math.Sqrt (reached through an extern alias) is not the BCL. The CLR runs
// that method, so the compiler must report an unlinked call instead of substituting f64.sqrt.
const identityProbe = "demos/stage-29-math-intrinsics/identity/IdentityProbe";
run("dotnet", ["build", "-c", "Release", identityProbe]);
const rejectedIdentity = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", identityProbe + "/bin/Release/netstandard2.0/IdentityProbe.dll", "IdentityProbe.Probe", "artifacts/unsupported-identity.wat"],
    { encoding: "utf8" });
assert.equal(rejectedIdentity.status, 1);
assert.match(rejectedIdentity.stderr, /Probe::Compute\(System\.Double\) at IL_[0-9a-f]+ \(call\): Unlinked call: System\.Double System\.Math::Sqrt\(System\.Double\) in ThirdPartyMath/);
// Host boundary: a JS caller can pass out-of-range Numbers to small-typed parameters, which the CLR cannot express.
// The module must see the value a CLR store of the same bits would produce.
{
    const { instance } = await WebAssembly.instantiate(readFileSync("artifacts/PrimitiveArrays.wasm"));
    const exports = instance.exports;
    assert.deepEqual([exports.ShortParameter(40000), exports.CharParameter(-1), exports.SByteParameter(200)], [-25536, 65535, -56]);
}
// Compound assignment to an array element needs a managed element pointer (ldelema), which is not lowered yet.
const arraysDemo = demos.find(d => d.assembly === "PrimitiveArrays");
const rejectedElement = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", arraysDemo.dll, "PrimitiveArrays.CompoundElement", "artifacts/unsupported-element.wat"], { encoding: "utf8" });
assert.equal(rejectedElement.status, 1);
assert.match(rejectedElement.stderr, /CompoundElement::Increment\(System\.Int32\) at IL_[0-9a-f]+ \(ldelema\)/);
// A third-party class named System.Int16 is not the CLI primitive, so arrays of it must not lower to short[]. The CLR keeps
// the two array types distinct (#75 review).
const arrayIdentityProbe = "demos/stage-37-primitive-arrays/identity/ArrayIdentityProbe";
run("dotnet", ["build", "-c", "Release", arrayIdentityProbe]);
const rejectedArrayIdentity = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler", "--",
    arrayIdentityProbe + "/bin/Release/netstandard2.0/ArrayIdentityProbe.dll", "ArrayIdentityProbe.Probe", "artifacts/unsupported-array-identity.wat"],
    { encoding: "utf8" });
assert.equal(rejectedArrayIdentity.status, 1);
assert.match(rejectedArrayIdentity.stderr, /Probe::Make\(System\.Int32\) at IL_[0-9a-f]+ \(newarr\): Arrays of System\.Int16 in ThirdPartyPrimitive/);
// #141: the oracle writes MathIntrinsics.dll with Culture set on its platform AssemblyRefs (name and token unchanged).
// The CLR does not bind that reference to the BCL, so its System.Double is not the CLI primitive.
const rejectedCulture = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler", "--",
    "artifacts/culture-probe/MathIntrinsics.dll", "MathIntrinsics.Measures", "artifacts/unsupported-culture.wat"],
    { encoding: "utf8" });
assert.equal(rejectedCulture.status, 1);
assert.match(rejectedCulture.stderr, /Measures::Sqrt\(System\.Double\) at IL_[0-9a-f]+ \([a-z.0-9]+\): Unsupported value type: System\.Double in netstandard/);
assert.equal(existsSync("artifacts/unsupported-culture.wat"), false);
const cases = JSON.parse(readFileSync("artifacts/oracle.json"));
console.log("Node:", await verify(name => readFileSync("artifacts/" + name + ".wasm"), cases,
    bytes => writeFileSync("artifacts/StandardWasm.Compiler.wasm", bytes)));

const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
});
try {
    const page = await browser.newPage();
    // Serve only these fixture assets to the isolated test page.
    const assets = new Map([
        ["/bootstrap.mjs", ["text/javascript", readFileSync("scripts/bootstrap.mjs")]],
        ["/invoke.mjs", ["text/javascript", readFileSync("scripts/invoke.mjs")]],
        ["/verify.mjs", ["text/javascript", readFileSync("scripts/verify.mjs")]],
        ...demos.map(d => ["/" + d.assembly, ["application/wasm", readFileSync(d.wasm)]])
    ]);
    await page.route("http://standardwasm.test/**", async route => {
        const path = new URL(route.request().url()).pathname;
        const [contentType, body] = assets.get(path) || ["text/html", "<title>StandardWasm verification</title>"];
        await route.fulfill({ contentType, body });
    });
    await page.goto("http://standardwasm.test/");
    const result = await page.evaluate(async cases => {
        const { verify } = await import("/verify.mjs");
        return verify(async name => (await fetch("/" + name)).arrayBuffer(), cases);
    }, cases);
    console.log("Chromium " + browser.version() + ":", result);
} finally {
    await browser.close();
}
