import { chromium } from "playwright-core";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { serveIntake } from "./server.mjs";
import { join } from "node:path";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "./loader/intake.mjs";

const serve = process.argv.includes("--serve");
if (!serve && process.platform !== "win32") throw Error("The Stage 72 fixture requires Windows.");
const { url, close } = await serveIntake(serve ? 5173 : 0);

if (serve) {
    console.log("Executable intake at " + url);
} else {
    try {
        await verify();
    } finally {
        await close();
    }
}

async function verify() {
    const reference = "demos/stage-72-winforms-windows-reference";
    const referenceRuns = join(root, "artifacts", "winforms-windows-reference");
    const prior = new Set(existsSync(referenceRuns) ? readdirSync(referenceRuns) : []);
    run(process.execPath, ["scripts/demo.mjs", "stage-72-winforms-windows-reference"]);
    const produced = readdirSync(referenceRuns).filter(path => !prior.has(path) && path.startsWith("run-"));
    assert.equal(produced.length, 1);
    const referenceDir = join(referenceRuns, produced[0]);
    const ordinaryDir = join(referenceDir, "ordinary");
    const singleDir = join(referenceDir, "single");
    const unbundled = join(root, reference, "App/bin/Release/net10.0-windows/win-x64");
    const app = "WinFormsReference";
    const exe = app + ".exe";
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    const read = (dir, path) => ({ path, bytes: readFileSync(join(dir, path)) });
    const ordinary = readdirSync(ordinaryDir).filter(path => statSync(join(ordinaryDir, path)).isFile())
        .map(path => read(ordinaryDir, path));
    const single = [read(singleDir, exe)];
    const result = value => ({
        packageType: value.packageType,
        entry: value.entry.path,
        entryToken: value.entryToken,
        sha256: hash(value.entry.bytes),
        depsSha256: hash(value.deps.bytes),
        configSha256: hash(value.config.bytes)
    });
    const normal = result(openExecutable(ordinary, exe));
    const managed = result(openExecutable(ordinary, app + ".dll"));
    const bundle = result(openExecutable(single, exe));
    assert.equal(normal.packageType, "apphost");
    assert.deepEqual(managed, { ...normal, packageType: "managed-pe" });
    assert.equal(bundle.packageType, "single-file");
    for (const [record, source] of [[normal, ordinaryDir], [bundle, unbundled]]) {
        assert.equal(record.entry, app + ".dll");
        assert.equal(record.sha256, hash(read(source, app + ".dll").bytes));
        assert.equal(record.depsSha256, hash(read(source, app + ".deps.json").bytes));
        assert.equal(record.configSha256, hash(read(source, app + ".runtimeconfig.json").bytes));
    }
    assert.throws(() => openExecutable([read(ordinaryDir, exe)], exe), /original publish companions/);
    const truncated = { path: exe, bytes: single[0].bytes.subarray(0, single[0].bytes.length - 10) };
    assert.throws(() => openExecutable([truncated], exe), /Truncated/);

    const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
    const browser = await chromium.launch({
        executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
    });
    try {
        const page = await browser.newPage();
        const methodErrors = [];
        const requests = [];
        page.on("pageerror", error => methodErrors.push(error.message));
        page.on("request", request => requests.push(request.method()));
        await page.goto(url);
        await page.waitForFunction(() => document.querySelector("#drop").style.border.length > 0);

        async function drop(dropped) {
            await page.locator("#status").evaluate(node => node.dataset.state = "pending");
            const transfer = await page.evaluateHandle(files => {
                const data = new DataTransfer();
                for (const file of files) data.items.add(new File([Uint8Array.from(file.bytes)], file.path));
                return data;
            }, dropped.map(file => ({ path: file.path, bytes: Array.from(file.bytes) })));
            try { await page.locator("#drop").dispatchEvent("drop", { dataTransfer: transfer }); }
            finally { await transfer.dispose(); }
            await page.waitForFunction(() => ["ready", "error"].includes(document.querySelector("#status").dataset.state));
            return page.locator("#status").textContent();
        }

        assert.match(await drop([read(ordinaryDir, exe)]), /original publish companions/);
        assert.deepEqual(JSON.parse(await drop(ordinary.filter(file => file.path !== exe))), normal);
        assert.deepEqual(JSON.parse(await drop(ordinary)), normal);
        assert.deepEqual(JSON.parse(await drop(single)), bundle);
        await page.locator("#status").evaluate(node => node.dataset.state = "pending");
        await page.locator("#folder").setInputFiles(ordinaryDir);
        await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
        assert.deepEqual(JSON.parse(await page.locator("#status").textContent()), normal);
        assert.deepEqual(methodErrors, []);
        assert.ok(requests.every(method => method === "GET"), "The drop UI must not upload application bytes.");
        writeFileSync(join(referenceDir, "intake.json"), JSON.stringify({ node: process.version,
            browser: browser.version(), ordinary: normal, single: bundle }, null, 2) + "\n");
        console.log("Node and Edge matched the original entry DLL and manifests for ordinary and single-file .NET executables.");
    } finally {
        await browser.close();
    }
}
