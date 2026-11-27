import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { serveLaunch } from "./launch-server.mjs";
import { resolveFrameworks } from "./frameworks.mjs";
import { runManagedEntry } from "./launch.mjs";

const stage = "demos/stage-74-executable-entry";
const compiler = join(root, "artifacts", "StandardWasm.Compiler.wasm");
const serve = process.argv.includes("--serve");
if (!serve) {
    if (process.platform !== "win32") throw Error("The executable comparison requires Windows.");
    // --no-rebuild reuses artifacts/StandardWasm.Compiler.wasm, so a focused check does not
    // repeat the full suite (owner direction: focused tests per change, milestone suites).
    if (!process.argv.includes("--no-rebuild")) run(process.execPath, ["scripts/test.mjs"]);
}
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

const { url, close } = await serveLaunch(serve ? 5174 : 0, compiler);
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
    // Issue #299: the framework-dependent desktop publish needs Microsoft.WindowsDesktop.App,
    // which this host cannot supply yet. As hostfxr, the launcher reports the missing framework
    // with exit status 0x80008096 before compiling, and Main never runs.
    assert.equal(nodeDesktop.state, "framework-missing");
    assert.equal(nodeDesktop.exitCode, 0x80008096 | 0);
    assert.equal(nodeDesktop.missingFramework.name, "Microsoft.WindowsDesktop.App");
    assert.ok(nodeDesktop.stderr.startsWith("You must install or update .NET to run this application.\r\n"));
    assert.equal(nodeDesktop.compilerStatus, undefined);

    // Issue #299 (Codex review of 4f86a51): supplied framework assets are the inventory. The same
    // desktop publish with its original Microsoft.WindowsDesktop.App DLLs must reach the compiler.
    const suppliedFramework = desktopFrameworkAssets();
    const nodeDesktopSupplied = await runManagedEntry(compilerBytes, {
        ...openExecutable(desktopFiles.map(path => ({ path, bytes: readFileSync(join(desktop, path)) })), desktopExe),
        framework: suppliedFramework
    });
    assert.notEqual(nodeDesktopSupplied.state, "framework-missing", JSON.stringify(nodeDesktopSupplied).slice(0, 300));

    // An exact prerelease with rollForward Disable: hostfxr refuses the installed stable 10.0.10
    // and never runs Main; the launcher must agree.
    const preview = join(evidence, "preview");
    mkdirSync(preview, { recursive: true });
    for (const path of ordinaryFiles) writeFileSync(join(preview, path), readFileSync(join(ordinary, path)));
    const previewConfig = JSON.parse(readFileSync(join(preview, "ConsoleReference.runtimeconfig.json"), "utf8"));
    previewConfig.runtimeOptions.rollForward = "Disable";
    previewConfig.runtimeOptions.framework.version = "10.0.10-preview.1";
    writeFileSync(join(preview, "ConsoleReference.runtimeconfig.json"), JSON.stringify(previewConfig, null, 2));
    const hostfxrPreview = spawnSync("dotnet", [join(preview, dll)], { windowsHide: true, timeout: 10000 });
    assert.equal(hostfxrPreview.error, undefined);
    assert.equal(hostfxrPreview.status | 0, 0x80008096 | 0, "hostfxr exit for the Disable prerelease");
    assert.match(hostfxrPreview.stderr.toString(), /^You must install or update \.NET to run this application\./);
    const nodePreview = await runNode(preview, ordinaryFiles, exe);
    assert.equal(nodePreview.state, "framework-missing");
    assert.equal(nodePreview.exitCode, hostfxrPreview.status | 0);
    assert.equal(nodePreview.missingFramework.version, "10.0.10-preview.1");

    // Policy precedence against real hostfxr: an entry's rollForward overrides the global one.
    const hostfxrMatrix = [
        ["entry Disable over global LatestPatch", { rollForward: "LatestPatch", framework: { name: "Microsoft.NETCore.App", version: "10.0.0", rollForward: "Disable" } }],
        ["entry LatestPatch over global Disable", { rollForward: "Disable", framework: { name: "Microsoft.NETCore.App", version: "10.0.0", rollForward: "LatestPatch" } }],
        ["exact prerelease Disable", { rollForward: "Disable", framework: { name: "Microsoft.NETCore.App", version: "10.0.10-preview.1" } }],
        ["default Minor", { framework: { name: "Microsoft.NETCore.App", version: "10.0.0" } }]
    ].map(([name, options], index) => {
        const config = { runtimeOptions: { tfm: "net10.0", ...options } };
        const file = join(evidence, "hostfxr-" + index + ".runtimeconfig.json");
        writeFileSync(file, JSON.stringify(config));
        const native = spawnSync("dotnet", ["exec", "--runtimeconfig", file, join(ordinary, dll)], { windowsHide: true, timeout: 10000 });
        assert.equal(native.error, undefined);
        const runs = native.status === clr.status;
        assert.ok(runs || (native.status | 0) === (0x80008096 | 0), name + ": hostfxr status " + native.status);
        assert.equal(resolveFrameworks(config).ok, runs, name);
        return { name, hostfxrRuns: runs };
    });

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
            await page.waitForFunction(() => ["finished", "unsupported", "framework-missing", "error"].includes(
                document.querySelector("#status").dataset.state));
            return JSON.parse(await page.locator("#status").textContent());
        }
        assert.deepEqual(await runEdge(ordinary, ordinaryFiles), nodeOrdinary);
        assert.deepEqual(await runEdge(single, [exe]), nodeSingle);
        assert.deepEqual(await runEdge(desktop, desktopFiles), nodeDesktop);
        assert.deepEqual(await runEdge(preview, ordinaryFiles), nodePreview);
        // The page's framework input supplies the same desktop assets.
        await page.locator("#framework").setInputFiles(suppliedFramework.map(asset => asset.source));
        const edgeDesktopSupplied = await runEdge(desktop, desktopFiles);
        await page.locator("#framework").setInputFiles([]);
        assert.notEqual(edgeDesktopSupplied.state, "framework-missing");
        assert.equal(edgeDesktopSupplied.state, nodeDesktopSupplied.state);
        assert.equal(edgeDesktopSupplied.compilerStatus, nodeDesktopSupplied.compilerStatus);
        assert.deepEqual(errors, []);
        writeFileSync(join(evidence, "results.json"), JSON.stringify({
            node: process.version, browser: browser.version(), clrExit: clr.status, nativeSingleExit: native.status,
            ordinary: nodeOrdinary, single: nodeSingle,
            winforms: { compiler: nodeDesktop, firstObjectLayoutBlocker: inspected },
            suppliedFramework: { node: { state: nodeDesktopSupplied.state, compilerStatus: nodeDesktopSupplied.compilerStatus,
                diagnostic: nodeDesktopSupplied.diagnostic }, edge: { state: edgeDesktopSupplied.state } },
            disablePrerelease: { hostfxrExit: hostfxrPreview.status | 0, node: nodePreview.state }, hostfxrMatrix
        }, null, 2) + "\n");
        console.log("Node and Edge ran both original console publishes with Windows exit parity and reported the missing Microsoft.WindowsDesktop.App framework as hostfxr does; " +
            "with its desktop framework supplied the publish reached the compiler (" + nodeDesktopSupplied.state + "), and a Disable prerelease matched hostfxr's 0x80008096.");
    } finally {
        await browser.close();
    }
}

// The installed desktop and shared framework DLLs a host would supply, as Stage 95 selects them.
function desktopFrameworkAssets() {
    const installed = spawnSync("dotnet", ["--list-runtimes"], { encoding: "utf8", windowsHide: true });
    if (installed.error || installed.status !== 0) throw Error("Could not list installed .NET runtimes.");
    const packs = new Map(installed.stdout.split(/\r?\n/).flatMap(line => {
        const match = /^([^ ]+) (10\.0\.[0-9]+) \[(.*)\]$/.exec(line);
        return match ? [[match[1], join(match[3], match[2])]] : [];
    }));
    const desktopPack = packs.get("Microsoft.WindowsDesktop.App"), shared = packs.get("Microsoft.NETCore.App");
    if (!desktopPack || !shared) throw Error("Install the .NET 10 desktop runtime.");
    const asset = (directory, path) => ({ path, source: join(directory, path), bytes: readFileSync(join(directory, path)) });
    return [
        ...["System.Windows.Forms.dll", "System.Windows.Forms.Primitives.dll", "System.Private.Windows.Core.dll",
            "System.Drawing.Common.dll"].map(path => asset(desktopPack, path)),
        ...["System.Private.CoreLib.dll", "System.Runtime.dll", "System.ComponentModel.Primitives.dll"].map(path => asset(shared, path))
    ];
}
