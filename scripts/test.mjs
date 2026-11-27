import assert from "node:assert/strict";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { decode, invoke, invokeGenerated, managedExceptionType } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { oracleCases } from "./oracle.mjs";
import { compile, invokeCompiler, root, run } from "./toolchain.mjs";
import { verify } from "./verify.mjs";
import { verifyBrowserSuite } from "./verify-browser-suite.mjs";
import { compareRuntimeVersions } from "./runtime-packs.mjs";

process.chdir(root);
// Runtime pack selection (decision 0044) picks the newest installed version.
assert.deepEqual(["10.0.10", "10.0.9", "10.0.0", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0-preview.7.1", "10.0.11-alpha"]
    .sort(compareRuntimeVersions),
    ["10.0.0-preview.7.1", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0", "10.0.9", "10.0.10", "10.0.11-alpha"]);
mkdirSync("artifacts", { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", "Verify", "--", "artifacts/oracle.json"]);
const demos = listDemos().filter(demo => !demo.runner);
for (const demo of demos) {
    compile(demo.dll, demo.exportType, demo.wasm);
    const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
    assert.equal((demo.generated
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
        : invoke(instance.exports, demo.method, demoInputs(demo))), decode(demo.expected), demo.stage);
}
const scalarDemo = demos.find(d => d.assembly === "Sample");
const wideDemo = demos.find(d => d.assembly === "LongIntegers");
const realsDemo = demos.find(d => d.assembly === "FloatingPoint");
const mathDemo = demos.find(d => d.assembly === "MathIntrinsics");
const arraysDemo = demos.find(d => d.assembly === "PrimitiveArrays");

// Select an ordinary non-ASCII CLR method name through the public CLI path.
compile(scalarDemo.dll, "Sample.ExportNames", "artifacts/ExportNames.wasm");
const { instance: namedExports } = await WebAssembly.instantiate(readFileSync("artifacts/ExportNames.wasm"));
assert.equal(namedExports.exports["Café"](7), 8);

// The portable compiler implements the checked operations that the earlier CLI rejected.
for (const [dll, type, member, input, expected, overflow] of [
    [scalarDemo.dll, "Sample.CheckedArithmetic", "Increment", 10, 11, 2147483647],
    [wideDemo.dll, "LongIntegers.CheckedWide", "Increment", 10n, 11n, 9223372036854775807n],
    [realsDemo.dll, "FloatingPoint.CheckedConversions", "Narrow", 10.75, 10, Infinity]
]) {
    const output = `artifacts/${type}.wasm`;
    compile(dll, type, output);
    const { instance } = await WebAssembly.instantiate(readFileSync(output));
    assert.equal(instance.exports[member](input), expected);
    assert.throws(() => instance.exports[member](overflow),
        error => managedExceptionType(instance.exports, error) === "System.OverflowException");
}

function assertUnsupported(dll, type, diagnostic) {
    const output = `artifacts/unsupported-${type}.wasm`;
    rmSync(output, { force: true });
    const rejected = invokeCompiler(dll, type, output, { encoding: "utf8" });
    if (rejected.error) throw rejected.error;
    assert.equal(rejected.status, 1);
    assert.match(rejected.stderr, diagnostic);
    assert.equal(existsSync(output), false);
}

// Unsupported members, non-platform identities and unsupported address forms remain diagnostics.
assertUnsupported(mathDemo.dll, "MathIntrinsics.UnmappedMath", /UnmappedMath::Power at IL_[0-9a-f]+ returned compiler status -2/);
const identityProbe = "demos/stage-29-math-intrinsics/identity/IdentityProbe";
run("dotnet", ["build", "-c", "Release", identityProbe]);
assertUnsupported(identityProbe + "/bin/Release/netstandard2.0/IdentityProbe.dll", "IdentityProbe.Probe",
    /Probe::Compute at IL_[0-9a-f]+ returned compiler status -2/);
const inlineArrays = demos.find(d => d.assembly === "InlineArrayLayouts");
assertUnsupported(inlineArrays.dll, "InlineArrayLayouts.LayoutBoundary",
    /LayoutBoundary::Read returned compiler status -2/);
const objectLocations = demos.find(d => d.assembly === "ManagedObjectByrefs");
assertUnsupported(objectLocations.dll, "ManagedObjectByrefs.HostBoundary",
    /HostBoundary::HostAlias returned compiler status -2/);
assertUnsupported(objectLocations.dll, "ManagedObjectByrefs.TypedBoundary",
    /TypedBoundary::TypedElement at IL_[0-9a-f]+ returned compiler status -2/);
const arrayIdentityProbe = "demos/stage-37-primitive-arrays/identity/ArrayIdentityProbe";
run("dotnet", ["build", "-c", "Release", arrayIdentityProbe]);
assertUnsupported(arrayIdentityProbe + "/bin/Release/netstandard2.0/ArrayIdentityProbe.dll", "ArrayIdentityProbe.Probe",
    /Probe::Make returned compiler status -2/);
assertUnsupported("artifacts/culture-probe/MathIntrinsics.dll", "MathIntrinsics.Measures",
    /Measures::Sqrt at IL_[0-9a-f]+ returned compiler status -2/);

// A host caller can supply numbers outside the CLI parameter's declared width.
{
    const { instance } = await WebAssembly.instantiate(readFileSync("artifacts/PrimitiveArrays.wasm"));
    const exports = instance.exports;
    assert.deepEqual([exports.ShortParameter(40000), exports.CharParameter(-1), exports.SByteParameter(200)], [-25536, 65535, -56]);
}
const rawOracle = JSON.parse(readFileSync("artifacts/oracle.json"));
const cases = oracleCases(rawOracle);
console.log("Node:", await verify(name => readFileSync("artifacts/" + name + ".wasm"), cases,
    bytes => writeFileSync("artifacts/StandardWasm.Compiler.wasm", bytes)));

const { result, version } = await verifyBrowserSuite(demos, rawOracle);
console.log("Chromium " + version + ":", result);
