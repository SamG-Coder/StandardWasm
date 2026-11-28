import assert from "node:assert/strict";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { availableParallelism } from "node:os";
import { join } from "node:path";
import { decode, invoke, invokeGenerated, managedExceptionType } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { oracleCases } from "./oracle.mjs";
import { readOracle } from "./oracle-store.mjs";
import { compileAll, compile, invokeCompiler, root, run } from "./toolchain.mjs";
import { planJobs, verifyConcurrently } from "./verify-jobs.mjs";
import { startNodePool } from "./verify-node.mjs";
import { verifyBrowserSuite } from "./verify-browser-suite.mjs";
import { compareRuntimeVersions } from "./runtime-packs.mjs";

process.chdir(root);
// Wall-clock time per phase, printed as it ends and summarized at exit (#316).
const suiteStart = performance.now();
const timings = [];
async function phase(name, body) {
    const started = performance.now();
    try {
        return await body();
    } finally {
        const seconds = (performance.now() - started) / 1000;
        timings.push({ name, seconds });
        console.log(`[phase] ${name}: ${seconds.toFixed(1)}s`);
    }
}
// Owner direction (#316): per-change suites verify in Edge, the shipping engine. A release
// (--release, or `npm run test:release`) also verifies the same jobs in Node.
const release = process.argv.includes("--release");
// Each engine runs this many jobs at once; with both engines they share the logical cores.
// STANDARDWASM_TEST_LANES overrides the per-engine count.
const lanes = Number(process.env.STANDARDWASM_TEST_LANES) ||
    Math.max(2, release ? Math.floor((availableParallelism() - 2) / 2) : availableParallelism() - 2);

// Runtime pack selection (decision 0044) picks the newest installed version.
assert.deepEqual(["10.0.10", "10.0.9", "10.0.0", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0-preview.7.1", "10.0.11-alpha"]
    .sort(compareRuntimeVersions),
    ["10.0.0-preview.7.1", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0", "10.0.9", "10.0.10", "10.0.11-alpha"]);
// Development aid: --verify-only reuses the oracle and modules of an earlier full run and
// repeats only the verification phases. A gate always runs the whole suite.
const verifyOnly = process.argv.includes("--verify-only");
mkdirSync("artifacts", { recursive: true });
if (!verifyOnly) await phase("Verify oracle (dotnet)", () =>
    run("dotnet", ["run", "-c", "Release", "--project", "Verify", "--", "artifacts/oracle.json"]));
const demos = listDemos().filter(demo => !demo.runner);
if (!verifyOnly) await phase("Demo compile", () =>
    compileAll(demos.map(demo => ({ dll: demo.dll, exportType: demo.exportType, output: demo.wasm })), availableParallelism()));
if (!verifyOnly) await phase("Demo checks", async () => {
    for (const demo of demos) {
        const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
        assert.equal((demo.generated
            ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
            : invoke(instance.exports, demo.method, demoInputs(demo))), decode(demo.expected), demo.stage);
    }
});
const scalarDemo = demos.find(d => d.assembly === "Sample");
const wideDemo = demos.find(d => d.assembly === "LongIntegers");
const realsDemo = demos.find(d => d.assembly === "FloatingPoint");
const mathDemo = demos.find(d => d.assembly === "MathIntrinsics");
const arraysDemo = demos.find(d => d.assembly === "PrimitiveArrays");

if (!verifyOnly) await phase("CLI checks", async () => {
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
    const { instance } = await WebAssembly.instantiate(readFileSync("artifacts/PrimitiveArrays.wasm"));
    const exports = instance.exports;
    assert.deepEqual([exports.ShortParameter(40000), exports.CharParameter(-1), exports.SByteParameter(200)], [-25536, 65535, -56]);
});

// The oracle is the case JSON plus one binary file of shared assembly images (oracle-store.mjs).
const oraclePath = join(root, "artifacts/oracle.json");
const { cases: rawCases, blobs } = readOracle(oraclePath);
const cases = oracleCases({ cases: rawCases, blobs });
// Comparisons stay grouped per module. Generated-code cases and the three compiler
// generations of decision 0004 each run as separate jobs (verify-jobs.mjs).
const jobs = planJobs(cases, 3, Math.max(1, lanes - 1));
const jobLog = engine => (job, seconds, error) =>
    console.log(`  [job] ${engine} ${job.kind}${job.generation ? " G" + job.generation : ""}` +
        `${job.indices ? " (" + job.indices.length + " cases)" : ""}: ${seconds.toFixed(1)}s${error ? " FAILED" : ""}`);

// Stage runners read the verified G2 compiler from this path.
const saveCompiler = bytes => writeFileSync("artifacts/StandardWasm.Compiler.wasm", bytes);

async function verifyNode() {
    const pool = startNodePool(oraclePath, join(root, "artifacts"), lanes, jobLog("Node"));
    try {
        return "Node: " + await verifyConcurrently(cases, jobs, pool.execute, saveCompiler);
    } finally {
        await pool.close();
    }
}

async function verifyBrowser() {
    const { result, version } = await verifyBrowserSuite(demos, oraclePath, cases, jobs, lanes, jobLog("Edge"),
        release ? undefined : saveCompiler);
    return "Chromium " + version + ": " + result;
}

// In a release Node and Edge verify the same jobs at the same time. Any failure fails the suite.
const summaries = await Promise.all([
    ...(release ? [phase("Node verify", verifyNode)] : []),
    phase("Edge verify", verifyBrowser)
]);
for (const summary of summaries) console.log(summary);
timings.push({ name: "Total", seconds: (performance.now() - suiteStart) / 1000 });
console.log(`Phase timings (${release ? "release: Node and Edge" : "Edge"}, ${lanes} lanes per engine):\n` +
    timings.map(({ name, seconds }) => `  ${name.padEnd(24)} ${seconds.toFixed(1).padStart(7)}s`).join("\n"));
writeFileSync("artifacts/test-timings.json", JSON.stringify({ release, lanes, timings }, null, 2));
