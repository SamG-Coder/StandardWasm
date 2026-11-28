import { spawn, spawnSync } from "node:child_process";
import { createWriteStream, mkdirSync, writeFileSync } from "node:fs";
import { availableParallelism } from "node:os";
import { join } from "node:path";
import { listDemos } from "./demos.mjs";
import { root, run } from "./toolchain.mjs";

// Runs every demo marked "runner" after `npm test` has saved artifacts/StandardWasm.Compiler.wasm.
// Usage: node scripts/runners.mjs [--release] [--lanes N] [--timeout seconds] [stage ...]
//
// Stages are independent except where they build the same project or another stage's outputs.
// Those run in fixed serial chains (below), each chain in its own lane; every other stage takes the
// next free lane. Per-change runs verify in Edge; --release sets STANDARDWASM_RELEASE=1 so the
// stages that support it also verify in Node (decision 0102).

// Builds or publishes another stage's projects, or reads its build outputs.
const executableChain = ["stage-72-winforms-windows-reference", "stage-73-executable-intake",
    "stage-74-executable-entry", "stage-110-console-arguments", "stage-151-interactive-stdin",
    "stage-112-standalone-webcil-intake", "stage-198-virtual-executable-directory",
    "stage-95-managed-companions", "stage-118-bundle-image-reuse", "stage-184-window-class-registration",
    "stage-187-window-class-bindings", "stage-194-win32-full-path"];
// Reference demos/stage-13-complete-bootstrap, or share the WinForms profile artifacts.
const bootstrapChain = ["stage-117-winforms-census", "stage-14-winforms-webgpu", "stage-32-winforms-textbox",
    "stage-34-shared-winforms-source", "stage-42-generated-profile", "stage-69-profile-text-uploads",
    "stage-70-profile-caption-redraw", "stage-17-dense-webgpu", "stage-152-finalization-root-tracing",
    "stage-160-native-receiver-demand"];

function command(stage) {
    switch (stage) {
        case "stage-74-executable-entry": case "stage-110-console-arguments": case "stage-151-interactive-stdin":
            return ["demos/" + stage + "/run.mjs", "--no-rebuild"];
        case "stage-14-winforms-webgpu": return ["scripts/winforms.mjs", "--verify"];
        case "stage-17-dense-webgpu": return ["scripts/dense-webgpu.mjs", "--verify"];
        case "stage-32-winforms-textbox": case "stage-34-shared-winforms-source":
            return ["scripts/winforms-textbox.mjs", "--verify"];
        case "stage-42-generated-profile": return ["scripts/winforms-textbox.mjs", "--verify", "--generated"];
        case "stage-69-profile-text-uploads":
            return ["scripts/winforms-textbox.mjs", "--generated", "--benchmark", "--regional"];
        case "stage-70-profile-caption-redraw":
            return ["scripts/winforms-textbox.mjs", "--generated", "--benchmark", "--region-redraw"];
        default: return ["scripts/demo.mjs", stage];
    }
}

const args = process.argv.slice(2);
const release = args.includes("--release");
const lanesArgument = args.indexOf("--lanes");
const lanes = lanesArgument >= 0 ? Number(args[lanesArgument + 1]) : Math.min(8, Math.max(2, availableParallelism() - 2));
const timeoutArgument = args.indexOf("--timeout");
// Seconds per stage. The slowest stage (117, the WinForms census) takes about 220 s.
const timeoutSeconds = timeoutArgument >= 0 ? Number(args[timeoutArgument + 1]) : 900;
const selected = args.filter((arg, i) => !arg.startsWith("--") && !["--lanes", "--timeout"].includes(args[i - 1]));
const stages = listDemos().filter(demo => demo.runner).map(demo => demo.stage)
    .filter(stage => selected.length === 0 || selected.includes(stage));

const logs = join(root, "artifacts", "runners");
mkdirSync(logs, { recursive: true });
// Every stage compiles through the same driver; build it once so their builds are skipped.
run("dotnet", ["build", "tools/PortableCompiler.Driver", "-c", "Release"]);

const chained = new Set([...executableChain, ...bootstrapChain]);
const chains = [executableChain, bootstrapChain].map(chain => chain.filter(stage => stages.includes(stage)));
const free = stages.filter(stage => !chained.has(stage));
const results = [];
const started = Date.now();

function runStage(stage) {
    const begin = Date.now();
    const log = createWriteStream(join(logs, stage + ".log"));
    const env = { ...process.env, ...(release ? { STANDARDWASM_RELEASE: "1" } : {}) };
    return new Promise(done => {
        const child = spawn(process.execPath, command(stage), { cwd: root, env, stdio: ["ignore", "pipe", "pipe"] });
        child.stdout.pipe(log, { end: false });
        child.stderr.pipe(log, { end: false });
        // A hung stage (a browser that never answers, a server that never exits) fails alone
        // instead of stalling its lane. The whole process tree goes, including Edge and dotnet.
        let timedOut = false;
        const timer = setTimeout(() => {
            timedOut = true;
            if (process.platform === "win32") spawnSync("taskkill", ["/pid", String(child.pid), "/T", "/F"], { stdio: "ignore" });
            else child.kill("SIGKILL");
        }, timeoutSeconds * 1000);
        child.on("close", status => {
            clearTimeout(timer);
            log.end();
            const seconds = Math.round((Date.now() - begin) / 1000);
            results.push({ stage, status: timedOut ? "timeout" : status, seconds });
            console.log(`${status === 0 && !timedOut ? "pass" : timedOut ? "FAIL timeout" : "FAIL"} ${stage} ${seconds}s`);
            done();
        });
    });
}

async function chainLane(chain) { for (const stage of chain) await runStage(stage); }
async function freeLane() { for (let stage = free.shift(); stage; stage = free.shift()) await runStage(stage); }

const chainLanes = chains.filter(chain => chain.length > 0);
await Promise.all([...chainLanes.map(chainLane),
    ...Array.from({ length: Math.max(1, lanes - chainLanes.length) }, freeLane)]);

const failed = results.filter(result => result.status !== 0);
const total = Math.round((Date.now() - started) / 1000);
writeFileSync(join(logs, "summary.json"), JSON.stringify({ release, lanes, total, results }, null, 2) + "\n");
console.log(`${results.length - failed.length}/${results.length} runners passed in ${total}s ` +
    `(${release ? "release: Edge and Node" : "Edge"}, ${lanes} lanes). Logs: artifacts/runners/`);
for (const { stage } of failed) console.log("FAIL " + stage + " (artifacts/runners/" + stage + ".log)");
process.exitCode = failed.length ? 1 : 0;
