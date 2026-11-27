import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { createConsoleHost, hex, inputFromBytes } from "../stage-110-console-arguments/host/console-host.mjs";
import { runConsoleEntry } from "../stage-110-console-arguments/host/console-launch.mjs";
import { serveInteractiveConsole } from "./host/server.mjs";

// Stage 151 (decision 0079): an unmodified .NET 10 console publish that calls
// Console.ReadLine runs on Windows, in Node and in Edge. Exit status and stdout/stderr
// bytes must agree for the same standard input. In Edge the lines are typed into the page
// after each prompt appears, while the page keeps running its own timer.
// --no-rebuild reuses artifacts/StandardWasm.Compiler.wasm from npm test.

// JS Promise Integration is unflagged in Edge but behind a flag in Node 24: rerun with it.
if (typeof WebAssembly.Suspending !== "function") {
    const child = spawnSync(process.execPath, ["--experimental-wasm-jspi", fileURLToPath(import.meta.url),
        ...process.argv.slice(2)], { stdio: "inherit" });
    process.exit(child.status ?? 1);
}

const stage = "demos/stage-151-interactive-stdin";
const compiler = join(root, "artifacts", "StandardWasm.Compiler.wasm");
const serve = process.argv.includes("--serve");
if (!serve) {
    if (process.platform !== "win32") throw Error("The console comparison requires Windows.");
    if (!process.argv.includes("--no-rebuild")) run(process.execPath, ["scripts/test.mjs"]);
}
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");

const { url, close } = await serveInteractiveConsole(serve ? 5175 : 0, compiler);
if (serve) console.log("Local interactive console at " + url + "/");
else {
    try { await verify(); }
    finally { await close(); }
}

// Without a console, .NET reads and writes through the ANSI code page (as Stage 110 found).
function windowsConsoleEncoding() {
    const query = spawnSync("reg", ["query", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Nls\\CodePage", "/v", "ACP"],
        { encoding: "utf8", windowsHide: true });
    const codePage = Number(/ACP\s+REG_SZ\s+(\d+)/.exec(query.stdout ?? "")?.[1]);
    const encoding = { 1252: "windows-1252", 65001: "utf-8" }[codePage];
    if (!encoding) throw Error("ANSI code page " + codePage + " has no modeled console encoding.");
    return { codePage, encoding };
}

// The stdin bytes for text, encoded as the console would receive them.
function inputBytes(text, consoleOptions) {
    const host = createConsoleHost(consoleOptions);
    for (let i = 0; i < text.length; i++) host.imports["standardwasm:console"].write(1, text.charCodeAt(i));
    return host.output().stdout;
}

// The interleaved stdout/stderr sequence App/Program.cs writes for these input lines, with
// adjacent writes to one stream merged. Separate per-stream byte comparisons cannot see
// cross-stream order, so the interactive page is checked against this.
function expectedTranscript(lines, newLine) {
    const chunks = [];
    const write = (stream, text) => {
        const last = chunks[chunks.length - 1];
        if (last && last.stream === stream) last.text += text;
        else chunks.push({ stream, text });
    };
    const prompt = () => { write(1, "name"); write(2, "!"); write(1, "? "); };
    prompt();
    for (const line of lines) {
        write(1, line.length === 0 ? "(empty)" + newLine : "hello " + line + newLine);
        prompt();
    }
    write(1, newLine);
    write(2, "eof" + newLine);
    return chunks;
}

function runWindows(executable, stdin, args = []) {
    return new Promise((resolve, reject) => {
        const child = spawn(executable, args, { detached: true, windowsHide: true, stdio: ["pipe", "pipe", "pipe"] });
        const stdout = [], stderr = [];
        const timer = setTimeout(() => child.kill(), 10000);
        child.stdout.on("data", chunk => stdout.push(chunk));
        child.stderr.on("data", chunk => stderr.push(chunk));
        child.on("error", reject);
        child.on("close", status => {
            clearTimeout(timer);
            resolve({ exitCode: status, stdout: hex(Buffer.concat(stdout)), stderr: hex(Buffer.concat(stderr)) });
        });
        child.stdin.end(Buffer.from(stdin));
    });
}

async function verify() {
    const base = join(root, "artifacts", "interactive-stdin");
    mkdirSync(base, { recursive: true });
    const evidence = mkdtempSync(join(base, "run-"));
    console.log("Interactive stdin evidence at " + evidence);
    const ordinary = join(evidence, "ordinary"), single = join(evidence, "single");
    const project = stage + "/App/InteractiveInput.csproj";
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-o", ordinary]);
    run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained",
        "-r", "win-x64", "-p:PublishSingleFile=true", "-o", single]);
    const exe = "InteractiveInput.exe";
    const { codePage, encoding } = windowsConsoleEncoding();
    const consoleOptions = { encoding, newLine: "\r\n" };

    // CRLF, LF and lone CR terminators, a final line without one, an empty line, EOF
    // straight away, and Unicode the code page can carry.
    const inputs = ["Ada\r\n\r\nGrace Hopper\r\n", "", "x", "one\ntwo\n", "a\rb\r\n", "Grüße\r\n"];
    if (encoding === "utf-8") inputs.push("\u{1f600} smile\r\n");

    const compilerBytes = readFileSync(compiler);
    const publishes = {
        apphost: { directory: ordinary, paths: readdirSync(ordinary) },
        singleFile: { directory: single, paths: [exe] }
    };
    const results = [];
    for (const text of inputs) {
        const stdin = inputBytes(text, consoleOptions);
        const row = { input: text, stdin: hex(stdin) };
        for (const [name, { directory, paths }] of Object.entries(publishes)) {
            const windows = await runWindows(join(directory, exe), stdin);
            const files = paths.map(path => ({ path, bytes: readFileSync(join(directory, path)) }));
            const node = await runConsoleEntry(compilerBytes, { ...openExecutable(files, exe), arguments: [],
                console: { ...consoleOptions, input: inputFromBytes(stdin, encoding) } });
            assert.equal(node.state, "finished", JSON.stringify(node));
            assert.ok(node.imports.includes("readLine"), "the entry must import readLine");
            assert.deepEqual({ exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr }, windows,
                "Node " + name + " " + JSON.stringify(text));
            row[name] = { windows, node: { exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr } };
        }
        results.push(row);
    }

    // Raw redirected bytes, decoded by the console encoding, printed back as UTF-16 code units
    // ("units" mode), so the stdout encoding cannot hide a decoding difference (Codex review
    // of 26fc3a4). The cases are: the five bytes code page 1252 leaves undefined, every byte
    // 0x80-0xff, and a leading UTF-8 BOM (U+FEFF on a UTF-8 console encoding, three
    // characters on code page 1252).
    const rawInputs = [
        [0x81, 0x8d, 0x8f, 0x90, 0x9d, 0x0a],
        [...Array.from({ length: 128 }, (_, i) => 0x80 + i).filter(byte => byte !== 0x0a && byte !== 0x0d), 0x0a],
        [0xef, 0xbb, 0xbf, 0x41, 0x0a]
    ];
    const rawResults = [];
    for (const bytes of rawInputs) {
        const row = { stdin: hex(Uint8Array.from(bytes)) };
        for (const [name, { directory, paths }] of Object.entries(publishes)) {
            const windows = await runWindows(join(directory, exe), bytes, ["units"]);
            const files = paths.map(path => ({ path, bytes: readFileSync(join(directory, path)) }));
            const node = await runConsoleEntry(compilerBytes, { ...openExecutable(files, exe), arguments: ["units"],
                console: { ...consoleOptions, input: inputFromBytes(Uint8Array.from(bytes), encoding) } });
            assert.equal(node.state, "finished", JSON.stringify(node));
            assert.deepEqual({ exitCode: node.exitCode, stdout: node.stdout, stderr: node.stderr }, windows,
                "Node raw " + name + " " + row.stdin);
            row[name] = { windows };
        }
        rawResults.push(row);
    }
    // The UTF-8 decoder keeps a leading BOM whatever this machine's code page is.
    assert.equal(await inputFromBytes(Uint8Array.from([0xef, 0xbb, 0xbf, 0x41, 0x0a]), "utf-8").next(), "﻿A");
    assert.equal(await inputFromBytes(Uint8Array.from([0x81, 0x9d, 0x0a]), "windows-1252").next(), "\u0081\u009d");

    // Edge: type the CRLF-terminated cases line by line, each after its prompt appears.
    const interactive = results.filter(row => /^([^\r\n]*\r\n)*$/.test(row.input));
    const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
    const browser = await chromium.launch({
        executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
    });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(url + "/");
        await page.selectOption("#encoding", encoding);
        await page.selectOption("#newline", "crlf");
        const prompts = async count => page.waitForFunction(n =>
            (document.querySelector("#stdout").textContent.match(/name\? /g) ?? []).length >= n, count);
        for (const row of interactive) {
            for (const [name, { directory, paths }] of Object.entries(publishes)) {
                await page.locator("#files").setInputFiles(paths.map(path => join(directory, path)));
                await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
                await page.getByRole("button", { name: "Run managed entry" }).click();
                const lines = row.input.split("\r\n").slice(0, -1);
                let waits = 0;
                for (let i = 0; i <= lines.length; i++) {
                    await prompts(i + 1);
                    // The prompt's stream transitions arrived in order before the read waits:
                    // stdout "name", stderr "!", stdout "? " (Codex review of 26fc3a4).
                    const tail = JSON.parse(await page.locator("#transcript").textContent()).slice(-3);
                    assert.deepEqual(tail.map(chunk => chunk.stream), [1, 2, 1], "Edge prompt order " + JSON.stringify(tail));
                    assert.ok(tail[0].text.endsWith("name") && tail[1].text === "!" && tail[2].text === "? ",
                        "Edge prompt chunks " + JSON.stringify(tail));
                    // A read is pending: the page timer must keep running.
                    const before = Number(await page.locator("#ticks").textContent());
                    await page.waitForFunction(b => Number(document.querySelector("#ticks").textContent) > b + 2, before);
                    waits++;
                    if (i < lines.length) {
                        await page.fill("#input", lines[i]);
                        await page.press("#input", "Enter");
                    }
                }
                await page.getByRole("button", { name: "End of input" }).click();
                await page.waitForFunction(() => ["finished", "unsupported", "unhandled-exception", "error"].includes(
                    document.querySelector("#status").dataset.state));
                const result = JSON.parse(await page.locator("#status").textContent());
                const edgeResult = { exitCode: result.exitCode, stdout: result.stdout, stderr: result.stderr };
                assert.deepEqual(edgeResult, row[name].windows, "Edge " + name + " " + JSON.stringify(row.input));
                // What the page showed while running is the same output, in order.
                const shown = await page.locator("#stdout").textContent();
                assert.equal(hex(inputBytes(shown, consoleOptions)), row[name].windows.stdout);
                // Across both streams, the delivered order is the program's write order.
                assert.deepEqual(JSON.parse(await page.locator("#transcript").textContent()),
                    expectedTranscript(lines, consoleOptions.newLine), "Edge stream order " + name + " " + JSON.stringify(row.input));
                row[name].edge = { ...edgeResult, responsiveWaits: waits };
            }
        }
        assert.deepEqual(errors, []);
        writeFileSync(join(evidence, "results.json"), JSON.stringify({
            node: process.version, browser: browser.version(), windowsAnsiCodePage: codePage, console: consoleOptions,
            results, rawResults
        }, null, 2) + "\n");
        console.log("Windows, Node and Edge agree on exit status and stdout/stderr bytes for " + results.length +
            " stdin inputs (" + interactive.length + " typed interactively in Edge, in stream order) and " + rawResults.length +
            " raw byte inputs (Windows and Node, as UTF-16 units), apphost and single-file publishes, code page " + codePage + ".");
    } finally {
        await browser.close();
    }
}
