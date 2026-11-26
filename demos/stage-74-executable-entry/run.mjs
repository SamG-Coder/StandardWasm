import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { serveIntake } from "../stage-73-executable-intake/server.mjs";
import { runManagedEntry } from "./launch.mjs";

const stage = "demos/stage-74-executable-entry";
const compiler = join(root, "artifacts", "StandardWasm.Compiler.wasm");
const serve = process.argv.includes("--serve");
if (!serve) {
    if (process.platform !== "win32") throw Error("The executable comparison requires Windows.");
    run(process.execPath, ["scripts/test.mjs"]);
}
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

const assets = new Map([
    ["/StandardWasm.Compiler.wasm", ["application/wasm", compiler]],
    ["/launch/launch.mjs", ["text/javascript", join(root, stage, "launch.mjs")]],
    ["/launch/launch-worker.mjs", ["text/javascript", join(root, stage, "launch-worker.mjs")]]
]);
const { url, close } = await serveIntake(serve ? 5174 : 0, assets);
const launchUrl = url + "/?run";
if (serve) console.log("Local executable launch at " + launchUrl);
else {
    try { await verify(); }
    finally { await close(); }
}

async function verify() {
    const base = join(root, "artifacts", "executable-entry");
    mkdirSync(base, { recursive: true });
    const evidence = mkdtempSync(join(base, "run-"));
    console.log("Executable entry evidence at " + evidence);
    const ordinary = join(evidence, "ordinary");
    const single = join(evidence, "single");
    const project = stage + "/Console/ConsoleReference.csproj";
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-o", ordinary]);
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained",
        "-r", "win-x64", "-p:PublishSingleFile=true", "-o", single]);
    const exe = "ConsoleReference.exe";
    const dll = "ConsoleReference.dll";
    const clr = spawnSync("dotnet", [join(ordinary, dll)], { windowsHide: true, timeout: 10000 });
    const native = spawnSync(join(single, exe), [], { windowsHide: true, timeout: 10000 });
    assert.equal(clr.error, undefined);
    assert.equal(native.error, undefined);
    assert.equal(clr.status, 37);
    assert.equal(native.status, clr.status);

    const windowsRuns = join(root, "artifacts", "winforms-windows-reference");
    const prior = new Set(existsSync(windowsRuns) ? readdirSync(windowsRuns) : []);
    run(process.execPath, ["scripts/demo.mjs", "stage-72-winforms-windows-reference"]);
    const produced = readdirSync(windowsRuns).filter(path => !prior.has(path) && path.startsWith("run-"));
    assert.equal(produced.length, 1);
    const desktop = join(windowsRuns, produced[0], "ordinary");
    const desktopExe = "WinFormsReference.exe";
    const inspector = spawnSync("dotnet", ["run", "-v", "quiet", "-c", "Release",
        "--project", stage + "/Inspector", "--", join(desktop, "WinFormsReference.dll")],
        { cwd: root, encoding: "utf8", windowsHide: true });
    if (inspector.error || inspector.status !== 0) throw Error(inspector.stderr, { cause: inspector.error });
    const inspected = JSON.parse(inspector.stdout);
    assert.equal(inspected.entryMethod, "Main");
    assert.equal(inspected.baseAssembly, "System.Windows.Forms");
    assert.equal(inspected.baseType, "System.Windows.Forms.Form");
    assert.equal(inspected.opcode, "newobj");

    const compilerBytes = readFileSync(compiler);
    async function runNode(directory, paths, entry) {
        const files = paths.map(path => ({ path, bytes: readFileSync(join(directory, path)) }));
        return runManagedEntry(compilerBytes, openExecutable(files, entry));
    }
    const ordinaryFiles = readdirSync(ordinary);
    const desktopFiles = readdirSync(desktop);
    const nodeOrdinary = await runNode(ordinary, ordinaryFiles, exe);
    const nodeSingle = await runNode(single, [exe], exe);
    const nodeDesktop = await runNode(desktop, desktopFiles, desktopExe);
    for (const result of [nodeOrdinary, nodeSingle]) {
        assert.equal(result.state, "finished");
        assert.equal(result.exitCode, clr.status);
        assert.equal(result.target, "net10.0");
    }
    assert.equal(nodeDesktop.state, "unsupported");
    assert.equal(nodeDesktop.compilerStatus, -2);

    const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
    const browser = await chromium.launch({
        executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
    });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(launchUrl);
        async function runEdge(dir, paths) {
            await page.locator("#files").setInputFiles(paths.map(path => join(dir, path)));
            await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
            await page.getByRole("button", { name: "Run managed entry" }).click();
            await page.waitForFunction(() => ["finished", "unsupported", "error"].includes(
                document.querySelector("#status").dataset.state));
            return JSON.parse(await page.locator("#status").textContent());
        }
        assert.deepEqual(await runEdge(ordinary, ordinaryFiles), nodeOrdinary);
        assert.deepEqual(await runEdge(single, [exe]), nodeSingle);
        assert.deepEqual(await runEdge(desktop, desktopFiles), nodeDesktop);
        assert.deepEqual(errors, []);
        writeFileSync(join(evidence, "results.json"), JSON.stringify({
            node: process.version, browser: browser.version(), clrExit: clr.status, nativeSingleExit: native.status,
            ordinary: nodeOrdinary, single: nodeSingle,
            winforms: { compiler: nodeDesktop, firstObjectLayoutBlocker: inspected }
        }, null, 2) + "\n");
        console.log("Node and Edge ran both original console publishes with Windows exit parity and reported the WinForms blocker.");
    } finally {
        await browser.close();
    }
}
