import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { runManagedEntry } from "../stage-74-executable-entry/launch.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-198-virtual-executable-directory");
const evidence = join(root, "artifacts/stage-198-virtual-executable-directory");
const publish = join(evidence, "publish");
const windowsDirectory = join(evidence, "working");
const exe = "VirtualDirectoryProbe.exe";
const dll = "VirtualDirectoryProbe.dll";
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
mkdirSync(windowsDirectory, { recursive: true });
run("dotnet", ["publish", join(stage, "App/VirtualDirectoryProbe.csproj"), "-c", "Release", "--no-self-contained", "-o", publish]);

const runtime = wasm32Runtime();
const native = spawnSync(runtime.host, ["exec", "--fx-version", runtime.version, join(publish, dll)], {
    cwd: windowsDirectory, windowsHide: true, encoding: "utf8"
});
if (native.error) throw native.error;
assert.equal(native.status, 0x5000 + windowsDirectory.length, native.stderr);

const files = readdirSync(publish).map(path => ({ path, bytes: readFileSync(join(publish, path)) }));
const selection = openExecutable(files, exe);
const node = await runManagedEntry(compiler, selection);
assert.equal(node.state, "finished", JSON.stringify(node));
assert.equal(node.exitCode, 0x5000 + "C:\\StandardWasm\\App".length);
const alternativeDirectory = "S:\\virtual\\package";
const override = await runManagedEntry(compiler, selection, { win32FileSystem: { currentDirectory: alternativeDirectory } });
assert.equal(override.state, "finished", JSON.stringify(override));
assert.equal(override.exitCode, 0x5000 + alternativeDirectory.length);

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url + "/?run");
    await page.locator("#files").setInputFiles(files.map(({ path }) => join(publish, path)));
    await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
    await page.getByRole("button", { name: "Run managed entry" }).click();
    await page.waitForFunction(() => ["finished", "unsupported", "framework-missing", "error"].includes(
        document.querySelector("#status").dataset.state));
    const status = page.locator("#status");
    const report = await status.textContent();
    assert.equal(await status.getAttribute("data-state"), "finished", report);
    const edge = JSON.parse(report);
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        clr: runtime.version, native: native.status, nodeVersion: process.version,
        edgeVersion: browser.version(), node, edge, override,
        source: files.map(({ path, bytes }) => ({ path, sha256: createHash("sha256").update(bytes).digest("hex") }))
    }, null, 2) + "\n");
    console.log("The original executable publish resolved relative paths in Windows x86, Node and the Edge launch worker.");
} finally {
    await browser?.close();
    await server.close();
}
