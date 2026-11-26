import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { basename, join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { runManagedEntry } from "../stage-74-executable-entry/launch.mjs";
import { packEntry, readManagedCompanions } from "../stage-74-executable-entry/companions.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (process.platform !== "win32") throw Error("The Windows executable comparison requires Windows.");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

const profile = process.argv.includes("--profile-bundle-images");
const evidenceRoot = join(root, "artifacts", profile ? "bundle-image-reuse" : "managed-companions");
mkdirSync(evidenceRoot, { recursive: true });
const evidence = mkdtempSync(join(evidenceRoot, "run-"));
const ordinary = join(evidence, "ordinary");
const single = join(evidence, "single");
const project = "demos/stage-95-managed-companions/App/LaunchProbe.csproj";
run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-o", ordinary]);
run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-r", "win-x64",
    "-p:PublishSingleFile=true", "-o", single]);
const entry = "LaunchProbe.exe";
const companion = "LaunchProbeDependency.dll";
const originalExit = windowsExit(join(ordinary, entry));
assert.equal(originalExit, 61);
assert.equal(windowsExit(join(single, entry)), originalExit);

const ordinaryFiles = readdirSync(ordinary).filter(path => !path.endsWith(".pdb")).map(path => file(ordinary, path));
const singleFiles = [file(single, entry)];
const dependency = ordinaryFiles.find(asset => asset.path === companion);
assert.ok(dependency);
const missing = ordinaryFiles.filter(asset => asset !== dependency);
const wrong = [...missing, { path: companion, bytes: readFileSync(join(ordinary, "LaunchProbe.dll")) }];
const invalid = [...missing, { path: companion, bytes: Buffer.from("not a managed PE") }];

const windowsRuns = join(root, "artifacts", "winforms-windows-reference");
const before = new Set(existsSync(windowsRuns) ? readdirSync(windowsRuns) : []);
run(process.execPath, ["scripts/demo.mjs", "stage-72-winforms-windows-reference"]);
const added = readdirSync(windowsRuns).filter(path => !before.has(path) && path.startsWith("run-"));
assert.equal(added.length, 1);
const desktopRun = join(windowsRuns, added[0]);
const desktopOutput = join(desktopRun, "ordinary");
const desktopFiles = readdirSync(desktopOutput).filter(path => !path.endsWith(".pdb")).map(path => file(desktopOutput, path));
const { framework, runtime } = desktopFramework(desktopRun);
const winformsBundle = profile ? describeBundle(desktopFiles, framework) : undefined;

const compilerBytes = readFileSync(compiler);
async function nodeLaunch(files, selected = entry, pack = []) {
    try {
        return await runManagedEntry(compilerBytes, { ...openExecutable(files, selected), framework: pack });
    } catch (error) {
        return { error: error.message };
    }
}
const cases = [
    ["ordinary", ordinaryFiles, entry, []],
    ["single", singleFiles, entry, []],
    ["missing", missing, entry, []],
    ["wrongIdentity", wrong, entry, []],
    ["notManaged", invalid, entry, []],
    ["duplicateIdentity", ordinaryFiles, entry, [{ ...dependency, path: "Duplicate.dll" }]],
    ["winforms", desktopFiles, "WinFormsReference.exe", framework],
];
const node = {};
const timingsMs = {};
for (const [name, files, selected, pack] of cases) {
    const started = performance.now();
    node[name] = await nodeLaunch(files, selected, pack);
    timingsMs[name] = { node: Math.round(performance.now() - started) };
}
for (const result of [node.ordinary, node.single]) {
    assert.equal(result.state, "finished");
    assert.equal(result.exitCode, originalExit);
}
assert.match(node.missing.error, /original managed companion: LaunchProbeDependency\.dll/);
assert.match(node.notManaged.error, /not a managed .NET assembly/);
assert.equal(node.wrongIdentity.compilerStatus, -4);
assert.equal(node.duplicateIdentity.compilerStatus, -5);
assert.equal(node.winforms.state, "unsupported");
assert.equal(node.winforms.compilerStatus, -2);

const server = await serveLaunch(0, compiler);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    for (const [name, files, , pack] of cases) {
        await page.goto(server.url + "/?run");
        await page.locator("#framework").setInputFiles(pack.map(payload));

        // Dispatch a real DOM drop from a DataTransfer so this exercises the
        // same intake event and worker as the user-facing page.
        const data = await page.evaluateHandle(items => {
            const transfer = new DataTransfer();
            for (const { path, bytes } of items)
                transfer.items.add(new File([new Uint8Array(bytes)], path, { type: "application/octet-stream" }));
            return transfer;
        }, files.map(asset => ({ path: asset.path, bytes: [...asset.bytes] })));
        await page.locator("#drop").dispatchEvent("drop", { dataTransfer: data });
        await data.dispose();
        const status = page.locator("#status");
        await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
        const started = performance.now();
        await page.getByRole("button", { name: "Run managed entry" }).click();
        await page.waitForFunction(() => ["finished", "unsupported", "error"].includes(
            document.querySelector("#status").dataset.state), null, { timeout: 610000 });
        timingsMs[name].edge = Math.round(performance.now() - started);
        const message = await status.textContent();
        const edge = await status.getAttribute("data-state") === "error" ? { error: message } : JSON.parse(message);
        assert.deepEqual(edge, node[name], name);
    }
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        node: process.version, edge: browser.version(), runtime, originalExit,
        compilerSha256: createHash("sha256").update(compilerBytes).digest("hex"),
        framework: framework.map(asset => asset.path), cases: node, timingsMs,
        winformsBundle
    }, null, 2) + "\n");
} finally {
    await browser?.close();
    await server.close();
}
console.log("Edge drop and Node matched both published Windows exits and all dependency diagnostics.");
if (profile) {
    console.log("WinForms bundle evidence " + JSON.stringify({ ...winformsBundle, timingsMs: timingsMs.winforms,
        diagnostic: node.winforms.diagnostic }));
    console.log("Evidence written to " + evidence);
}

function describeBundle(files, framework) {
    const selection = openExecutable(files, "WinFormsReference.exe");
    const deps = JSON.parse(selection.deps.bytes.toString("utf8"));
    const companions = readManagedCompanions({ ...selection, framework }, deps, deps.runtimeTarget.name);
    const [packed] = packEntry(selection.entry, companions, selection.entryToken & 0xffffff);
    const images = companions.length + 1;
    return {
        sha256: createHash("sha256").update(packed).digest("hex"),
        managedDllSha256: createHash("sha256").update(selection.entry.bytes).digest("hex"),
        images,
        packedBytes: packed.length,
        imageBytes: packed.length - 4 * images
    };
}

function windowsExit(path) {
    const result = spawnSync(path, [], { windowsHide: true, timeout: 10000 });
    if (result.error) throw result.error;
    return result.status;
}

function file(directory, path) {
    return { path, bytes: readFileSync(join(directory, path)) };
}

function payload({ path, bytes }) {
    return { name: basename(path), mimeType: "application/octet-stream", buffer: bytes };
}

function desktopFramework(output) {
    const { runtime } = JSON.parse(readFileSync(join(output, "windows.json"), "utf8"));
    const version = runtime.split(" ").at(-1);
    const installed = spawnSync("dotnet", ["--list-runtimes"], { encoding: "utf8", windowsHide: true });
    if (installed.error || installed.status !== 0) throw Error("Could not list installed .NET runtimes.");
    const packs = new Map(installed.stdout.split(/\r?\n/).flatMap(line => {
        const match = /^([^ ]+) ([^ ]+) \[(.*)\]$/.exec(line);
        return match?.[2] === version ? [[match[1], join(match[3], version)]] : [];
    }));
    const shared = packs.get("Microsoft.NETCore.App");
    const desktop = packs.get("Microsoft.WindowsDesktop.App");
    if (!shared || !desktop) throw Error("The recorded .NET desktop framework version is unavailable.");
    const framework = [
        ...["System.Windows.Forms.dll", "System.Windows.Forms.Primitives.dll", "System.Private.Windows.Core.dll",
            "System.Drawing.Common.dll", "System.Private.Windows.GdiPlus.dll"].map(path => file(desktop, path)),
        ...["System.Private.CoreLib.dll", "System.Runtime.dll", "System.ComponentModel.Primitives.dll", "System.Collections.dll",
            "System.Threading.dll", "System.Drawing.Primitives.dll", "System.ComponentModel.dll",
            "System.ComponentModel.TypeConverter.dll", "System.Collections.Concurrent.dll", "System.Collections.Specialized.dll",
            "System.ObjectModel.dll"].map(path => file(shared, path)),
    ];
    return { framework, runtime };
}
