import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { packEntry, readManagedCompanions } from "../stage-74-executable-entry/companions.mjs";
import { runManagedEntry } from "../stage-74-executable-entry/launch.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const executable = "WinFormsReference.exe";
const desktopNames = [
    "System.Windows.Forms", "System.Windows.Forms.Primitives", "System.Private.Windows.Core",
    "System.Drawing.Common", "System.Private.Windows.GdiPlus"
];
const coreNames = [
    "System.Private.CoreLib", "System.Runtime", "System.ComponentModel.Primitives", "System.Collections",
    "System.Threading", "System.Drawing.Primitives", "System.ComponentModel", "System.ComponentModel.TypeConverter",
    "System.Collections.Concurrent", "System.Collections.Specialized", "System.ObjectModel", "System.Threading.Thread"
];
const hash = bytes => createHash("sha256").update(bytes).digest("hex");
const asset = (directory, path) => ({ path, bytes: readFileSync(join(directory, path)) });

export async function verifyWinFormsStartup(reference, runtime, compilerPath) {
    const windows = JSON.parse(readFileSync(join(reference, "windows.json"), "utf8"));
    assert.equal(windows.runtime, ".NET " + runtime.version, "Use matching Windows and x86 runtimes.");
    const installed = execFileSync(runtime.host, ["--list-runtimes"], { encoding: "utf8" });
    const desktop = [...installed.matchAll(/^Microsoft\.WindowsDesktop\.App (\S+) \[(.+)\]$/gm)]
        .find(([, version]) => version === runtime.version);
    if (!desktop) throw Error("Install the matching win-x86 Windows Desktop runtime.");
    const framework = [
        ...desktopNames.map(name => asset(join(desktop[2], runtime.version), name + ".dll")),
        ...coreNames.map(name => asset(runtime.directory, name + ".dll"))
    ];
    const cases = [
        ["ordinary", windows.ordinaryLaunch, windows.ordinaryFiles],
        ["single", windows.bundledLaunch, windows.bundledFiles]
    ];
    const compiler = readFileSync(compilerPath);
    const server = await serveLaunch(0, compilerPath);
    let browser;
    try {
        browser = await chromium.launch({ channel: "msedge" });
        const page = await browser.newPage();
        const results = {};
        for (const [publish, launch, inventory] of cases) {
            assert.equal(launch.exitCode, 0, "The original executable must run on Windows.");
            const files = inventory.map(({ path, sha256 }) => {
                const original = asset(join(reference, publish), path);
                assert.equal(hash(original.bytes), sha256, publish + "/" + path);
                return original;
            });
            const selection = { ...openExecutable(files, executable), framework };
            const deps = JSON.parse(selection.deps.bytes.toString("utf8"));
            const companions = readManagedCompanions(selection, deps, deps.runtimeTarget.name);
            const [bundle] = packEntry(selection.entry, companions, selection.entryToken & 0xffffff, true);
            const node = await runManagedEntry(compiler, selection);
            // This historical stage establishes a located startup verdict before rendering.
            assert.deepEqual([node.state, node.compilerStatus, node.diagnostic], ["unsupported", -1, [0, 4, 44]]);

            await page.goto(server.url + "/?run");
            await page.locator("#framework").setInputFiles(framework.map(({ path, bytes }) =>
                ({ name: path, mimeType: "application/octet-stream", buffer: bytes })));
            const transfer = await page.evaluateHandle(items => {
                const data = new DataTransfer();
                for (const { path, bytes } of items)
                    data.items.add(new File([Uint8Array.from(bytes)], path));
                return data;
            }, files.map(({ path, bytes }) => ({ path, bytes: Array.from(bytes) })));
            try {
                await page.locator("#drop").dispatchEvent("drop", { dataTransfer: transfer });
            } finally {
                await transfer.dispose();
            }
            await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
            await page.getByRole("button", { name: "Run managed entry" }).click();
            await page.waitForFunction(() => ["finished", "unsupported", "error"].includes(
                document.querySelector("#status").dataset.state), null, { timeout: 610000 });
            const edge = JSON.parse(await page.locator("#status").textContent());
            assert.deepEqual(edge, node, publish);
            results[publish] = { launch, inventory, bundleSha256: hash(bundle), node, edge };
        }
        return { node: process.version, edge: browser.version(), compilerSha256: hash(compiler), results };
    } finally {
        await browser?.close();
        await server.close();
    }
}
