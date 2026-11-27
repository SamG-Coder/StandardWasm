import assert from "node:assert/strict";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { availableParallelism } from "node:os";
import { join } from "node:path";
import { decode, invoke, invokeGenerated, managedExceptionType } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { chromium } from "playwright-core";
import { oracleCases } from "./oracle.mjs";
import { blobPath, readOracle } from "./oracle-store.mjs";
import { compileAll, compile, invokeCompiler, root, run } from "./toolchain.mjs";
import { planJobs, verifyConcurrently } from "./verify-jobs.mjs";
import { startNodePool } from "./verify-node.mjs";
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
// Each engine runs this many jobs at once. Node and Edge run side by side, so together
// they use about every logical core. STANDARDWASM_TEST_LANES overrides the per-engine count.
const lanes = Number(process.env.STANDARDWASM_TEST_LANES) || Math.max(2, Math.floor((availableParallelism() - 2) / 2));

// Runtime pack selection (decision 0044) picks the newest installed version.
assert.deepEqual(["10.0.10", "10.0.9", "10.0.0", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0-preview.7.1", "10.0.11-alpha"]
    .sort(compareRuntimeVersions),
    ["10.0.0-preview.7.1", "10.0.0-rc.2.25502.107", "10.0.0-rc.10.1", "10.0.0", "10.0.9", "10.0.10", "10.0.11-alpha"]);
// Development aid: --verify-only reuses the oracle and modules of an earlier full run and
// repeats only the Node and Edge verification. A gate always runs the whole suite.
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

    // Unsupported members, non-platform identities and managed element addresses remain diagnostics.
    assertUnsupported(mathDemo.dll, "MathIntrinsics.UnmappedMath", /UnmappedMath::Power at IL_[0-9a-f]+ returned compiler status -2/);
    const identityProbe = "demos/stage-29-math-intrinsics/identity/IdentityProbe";
    run("dotnet", ["build", "-c", "Release", identityProbe]);
    assertUnsupported(identityProbe + "/bin/Release/netstandard2.0/IdentityProbe.dll", "IdentityProbe.Probe",
        /Probe::Compute at IL_[0-9a-f]+ returned compiler status -2/);
    assertUnsupported(arraysDemo.dll, "PrimitiveArrays.CompoundElement",
        /CompoundElement::Increment at IL_[0-9a-f]+ returned compiler status -2/);
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

async function verifyNode() {
    const pool = startNodePool(oraclePath, join(root, "artifacts"), lanes, jobLog("Node"));
    try {
        return "Node: " + await verifyConcurrently(cases, jobs, pool.execute,
            bytes => writeFileSync("artifacts/StandardWasm.Compiler.wasm", bytes));
    } finally {
        await pool.close();
    }
}

async function verifyBrowser() {
    const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
    // Node worker threads default to a 4 MB stack. Give each renderer's V8 the same limit:
    // with every core busy, tier-up lags and the compiler's recursion over a 236-deep type
    // argument (BootstrapLibrary RecursiveTypeArgument) still runs in Liftoff frames, which
    // overflow the default ~1 MB limit (#316, #318). The renderer thread stack is larger.
    const browser = await chromium.launch({
        executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined),
        args: ["--js-flags=--stack-size=3800"]
    });
    try {
        // Serve only these fixture assets to the isolated test pages.
        const assets = new Map([
            ["/oracle.json", ["application/json", readFileSync(oraclePath)]],
            ["/blobs.bin", ["application/octet-stream", readFileSync(blobPath(oraclePath))]],
            ["/oracle.mjs", ["text/javascript", readFileSync("scripts/oracle.mjs")]],
            ["/invoke.mjs", ["text/javascript", readFileSync("scripts/invoke.mjs")]],
            ["/verify-jobs.mjs", ["text/javascript", readFileSync("scripts/verify-jobs.mjs")]],
            ...demos.map(d => ["/" + d.assembly, ["application/wasm", readFileSync(d.wasm)]])
        ]);
        const serve = async route => {
            const path = new URL(route.request().url()).pathname;
            const [contentType, body] = assets.get(path) || ["text/html", "<title>StandardWasm verification</title>"];
            await route.fulfill({ contentType, body });
        };
        // One browser context per lane gives each its own renderer process and main thread.
        const pages = await Promise.all(Array.from({ length: lanes }, async () => {
            const context = await browser.newContext();
            await context.route("http://standardwasm.test/**", serve);
            const page = await context.newPage();
            await page.goto("http://standardwasm.test/");
            await page.evaluate(async () => {
                const { oracleCases } = await import("/oracle.mjs");
                const oracle = await (await fetch("/oracle.json")).json();
                const bytes = new Uint8Array(await (await fetch("/blobs.bin")).arrayBuffer());
                let offset = 0;
                oracle.blobs = oracle.blobLengths.map(length => bytes.subarray(offset, offset += length));
                globalThis.verificationCases = oracleCases(oracle);
                globalThis.verificationJobs = await import("/verify-jobs.mjs");
            });
            return page;
        }));
        const idle = [...pages], waiting = [];
        const log = jobLog("Edge");
        const execute = async job => {
            const page = idle.pop() ?? await new Promise(resume => waiting.push(resume));
            const started = performance.now();
            try {
                const result = await page.evaluate(job => globalThis.verificationJobs.runJob(job,
                    async name => (await fetch("/" + name)).arrayBuffer(), globalThis.verificationCases), job);
                log(job, (performance.now() - started) / 1000);
                return result;
            } catch (error) {
                log(job, (performance.now() - started) / 1000, error);
                throw error;
            } finally {
                const next = waiting.shift();
                if (next) next(page); else idle.push(page);
            }
        };
        return "Chromium " + browser.version() + ": " + await verifyConcurrently(cases, jobs, execute);
    } finally {
        await browser.close();
    }
}

// Node and Edge verify the same jobs at the same time. Either failure fails the suite.
const [nodeSummary, browserSummary] = await Promise.all([
    phase("Node verify", verifyNode),
    phase("Edge verify", verifyBrowser)
]);
console.log(nodeSummary);
console.log(browserSummary);
timings.push({ name: "Total", seconds: (performance.now() - suiteStart) / 1000 });
console.log("Phase timings (" + lanes + " lanes per engine):\n" +
    timings.map(({ name, seconds }) => `  ${name.padEnd(24)} ${seconds.toFixed(1).padStart(7)}s`).join("\n"));
writeFileSync("artifacts/test-timings.json", JSON.stringify({ lanes, timings }, null, 2));
