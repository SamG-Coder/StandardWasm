import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { createConsoleHost, hex } from "./host/console-host.mjs";
import { runConsoleEntry } from "./host/console-launch.mjs";
import { serveConsoleLaunch } from "./host/server.mjs";

// Runs an unmodified .NET 10 console publish with Main(string[]) on Windows,
// in Node and in Edge, and compares exit status and stdout/stderr bytes.
// --no-rebuild reuses artifacts/StandardWasm.Compiler.wasm from npm test.
const stage = "demos/stage-110-console-arguments";
const compiler = join(root, "artifacts", "StandardWasm.Compiler.wasm");
const serve = process.argv.includes("--serve");
if (!serve) {
    if (process.platform !== "win32") throw Error("The console comparison requires Windows.");
    if (!process.argv.includes("--no-rebuild")) run(process.execPath, ["scripts/test.mjs"]);
}
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

const { url, close } = await serveConsoleLaunch(serve ? 5174 : 0, compiler);
const launchUrl = url + "/";
if (serve) console.log("Local executable launch at " + launchUrl);
else {
    try {
        assert.throws(() => createConsoleHost({ encoding: "constructor" }), /Unsupported console encoding/);
        await verify();
    }
    finally { await close(); }
}

// A process without a console writes through Console.OutputEncoding, which
// .NET takes from the ANSI code page. Map it to the encoding the host models.
function windowsConsoleEncoding() {
    const query = spawnSync("reg", ["query", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Nls\\CodePage", "/v", "ACP"],
        { encoding: "utf8", windowsHide: true });
    const codePage = Number(/ACP\s+REG_SZ\s+(\d+)/.exec(query.stdout ?? "")?.[1]);
    const encoding = { 1252: "windows-1252", 65001: "utf-8" }[codePage];
    if (!encoding) throw Error("ANSI code page " + codePage + " has no modeled console encoding.");
    return { codePage, encoding };
}

// Detached: the child has no console, so its encoding does not depend on the
// code page of the terminal that started this script.
function runWindows(executable, args) {
    return new Promise((resolve, reject) => {
        const child = spawn(executable, args, { detached: true, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
        const stdout = [], stderr = [];
        const timer = setTimeout(() => child.kill(), 10000);
        child.stdout.on("data", chunk => stdout.push(chunk));
        child.stderr.on("data", chunk => stderr.push(chunk));
        child.on("error", reject);
        child.on("close", status => {
            clearTimeout(timer);
            resolve({ exitCode: status, stdout: hex(Buffer.concat(stdout)), stderr: hex(Buffer.concat(stderr)) });
        });
    });
}

async function verify() {
    const base = join(root, "artifacts", "console-arguments");
    mkdirSync(base, { recursive: true });
    const evidence = mkdtempSync(join(base, "run-"));
    console.log("Console argument evidence at " + evidence);
    const ordinary = join(evidence, "ordinary"), single = join(evidence, "single");
    const project = stage + "/App/ConsoleArguments.csproj";
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-o", ordinary]);
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained",
        "-r", "win-x64", "-p:PublishSingleFile=true", "-o", single]);
    const exe = "ConsoleArguments.exe";
    const { codePage, encoding } = windowsConsoleEncoding();
    const consoleOptions = { encoding, newLine: "\r\n" };
    const expected = JSON.parse(readFileSync(join(root, stage, "expected.json"), "utf8"));

    const compilerBytes = readFileSync(compiler);
    const publishes = {
        apphost: { directory: ordinary, paths: readdirSync(ordinary) },
        singleFile: { directory: single, paths: [exe] }
    };
    const results = [];
    for (const { arguments: args, exitCode, stdout, stderr } of expected) {
        // The expected text is what Main writes. Its bytes come from the
        // modeled encoder, which the Windows processes then confirm.
        const units = text => Array.from({ length: text.length }, (_, i) => text.charCodeAt(i));
        const host = createConsoleHost(consoleOptions);
        units(stdout).forEach(unit => host.imports["standardwasm:console"].write(1, unit));
        units(stderr).forEach(unit => host.imports["standardwasm:console"].write(2, unit));
        const bytes = host.output();
        const want = { exitCode, stdout: hex(bytes.stdout), stderr: hex(bytes.stderr) };
        const row = { arguments: args };
        for (const [name, { directory, paths }] of Object.entries(publishes)) {
            const windows = await runWindows(join(directory, exe), args);
            assert.deepEqual(windows, want, "Windows " + name + " " + JSON.stringify(args));
            const files = paths.map(path => ({ path, bytes: readFileSync(join(directory, path)) }));
            const node = await runConsoleEntry(compilerBytes,
                { ...openExecutable(files, exe), arguments: args, console: consoleOptions });
            assert.equal(node.state, "finished", JSON.stringify(node));
            assert.equal(node.target, "net10.0");
            assert.deepEqual({ exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr }, windows,
                "Node " + name + " " + JSON.stringify(args));
            row[name] = { windows, node };
        }
        results.push(row);
    }

    const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
    const browser = await chromium.launch({
        executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
    });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(launchUrl);
        await page.selectOption("#encoding", encoding);
        await page.selectOption("#newline", "crlf");
        for (const row of results) {
            for (const [name, { directory, paths }] of Object.entries(publishes)) {
                await page.locator("#files").setInputFiles(paths.map(path => join(directory, path)));
                await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
                await page.fill("#arguments", JSON.stringify(row.arguments));
                await page.getByRole("button", { name: "Run managed entry" }).click();
                await page.waitForFunction(() => ["finished", "unsupported", "unhandled-exception", "error"].includes(
                    document.querySelector("#status").dataset.state));
                const result = JSON.parse(await page.locator("#status").textContent());
                assert.deepEqual(result, row[name].node, "Edge " + name + " " + JSON.stringify(row.arguments));
                row[name].edge = { exitCode: result.exitCode, stdout: result.stdout, stderr: result.stderr };
            }
        }
        assert.deepEqual(errors, []);
        writeFileSync(join(evidence, "results.json"), JSON.stringify({
            node: process.version, browser: browser.version(), windowsAnsiCodePage: codePage, console: consoleOptions,
            results
        }, null, 2) + "\n");
        console.log("Windows, Node and Edge agree on exit status and stdout/stderr bytes for " +
            results.length + " argument sets, apphost and single-file publishes, code page " + codePage + ".");
    } finally {
        await browser.close();
    }
}
