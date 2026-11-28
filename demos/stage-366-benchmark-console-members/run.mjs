import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { hex } from "../stage-110-console-arguments/host/console-host.mjs";
import { runConsoleEntry } from "../stage-110-console-arguments/host/console-launch.mjs";
import { serveConsoleLaunch } from "../stage-110-console-arguments/host/server.mjs";

// Stage 366 (decision 0104): an ordinary .NET 10 console app that uses the platform members
// benchmark drivers need. The win-x86 apphost runs on the 32-bit CLR, whose native word matches
// wasm32, and the self-hosted compiler runs the same publish in Node and Edge. Exit status,
// stdout and stderr must agree byte for byte for each argument set.
// --no-rebuild reuses artifacts/StandardWasm.Compiler.wasm from npm test.
const stage = "demos/stage-366-benchmark-console-members";
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (process.platform !== "win32") throw Error("The CLR comparison requires Windows and the x86 .NET runtime.");
if (!process.argv.includes("--no-rebuild")) run(process.execPath, ["scripts/test.mjs"]);
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

// The app writes "\n" itself; the hosts below model the same UTF-8 output.
const consoleOptions = { encoding: "utf-8", newLine: "\r\n" };
const argumentSets = [[], ["exit"]];

function runWindows(executable, args) {
    return new Promise((resolve, reject) => {
        const child = spawn(executable, args, { detached: true, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
        const stdout = [], stderr = [];
        const timer = setTimeout(() => child.kill(), 60000);
        child.stdout.on("data", chunk => stdout.push(chunk));
        child.stderr.on("data", chunk => stderr.push(chunk));
        child.on("error", reject);
        child.on("close", status => {
            clearTimeout(timer);
            resolve({ exitCode: status, stdout: hex(Buffer.concat(stdout)), stderr: hex(Buffer.concat(stderr)) });
        });
    });
}

const base = join(root, "artifacts", "benchmark-console-members");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
console.log("Benchmark console member evidence at " + evidence);
const publish = join(evidence, "publish");
run("dotnet", ["publish", stage + "/App/BenchmarkMembers.csproj", "-c", "Release", "--no-self-contained",
    "-r", "win-x86", "-o", publish]);
const exe = "BenchmarkMembers.exe";
const paths = readdirSync(publish).filter(path => !path.endsWith(".pdb"));
const files = paths.map(path => ({ path, bytes: readFileSync(join(publish, path)) }));
const compilerBytes = readFileSync(compiler);

const results = [];
for (const args of argumentSets) {
    const windows = await runWindows(join(publish, exe), args);
    // Windows writes stdout bytes as UTF-8 only when told to; the app's text is ASCII, so the
    // bytes are the same in every code page.
    const node = await runConsoleEntry(compilerBytes, { ...openExecutable(files, exe), arguments: args, console: consoleOptions });
    assert.equal(node.state, "finished", JSON.stringify(node));
    assert.deepEqual({ exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr }, windows,
        "Node " + JSON.stringify(args));
    results.push({ arguments: args, windows, node: { exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr } });
}

const { url, close } = await serveConsoleLaunch(0, compiler);
const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({ executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined) });
try {
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(url + "/");
    await page.selectOption("#encoding", consoleOptions.encoding);
    await page.selectOption("#newline", "crlf");
    for (const row of results) {
        await page.locator("#files").setInputFiles(paths.map(path => join(publish, path)));
        await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
        await page.fill("#arguments", JSON.stringify(row.arguments));
        await page.getByRole("button", { name: "Run managed entry" }).click();
        await page.waitForFunction(() => ["finished", "unsupported", "unhandled-exception", "error"].includes(
            document.querySelector("#status").dataset.state));
        const result = JSON.parse(await page.locator("#status").textContent());
        const observed = { exitCode: result.exitCode, stdout: result.stdout, stderr: result.stderr };
        assert.deepEqual(observed, row.node, "Edge " + JSON.stringify(row.arguments));
        row.edge = observed;
    }
    assert.deepEqual(errors, []);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        node: process.version, browser: browser.version(), compiler, results
    }, null, 2) + "\n");
    console.log("Windows x86, Node and Edge agree on exit status and stdout/stderr bytes for " +
        results.length + " argument sets.");
} finally {
    await browser.close();
    await close();
}
