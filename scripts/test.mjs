import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { decode, invoke, invokeGenerated } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { chromium } from "playwright-core";
import { compile, root, run } from "./toolchain.mjs";
import { verify } from "./verify.mjs";

process.chdir(root);
mkdirSync("artifacts", { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", "Verify", "--", "artifacts/oracle.json"]);
const demos = listDemos().filter(demo => !demo.runner);
for (const demo of demos) {
    compile(demo.dll, demo.exportType, demo.wasm);
    const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
    assert.equal((demo.generated
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
        : invoke(instance.exports, demo.method, demoInputs(demo))), decode(demo.expected), demo.stage);
}
const scalarDemo = demos.find(d => d.assembly === "Sample");
const rejected = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", scalarDemo.dll, "Sample.CheckedArithmetic", "artifacts/unsupported.wat"], { encoding: "utf8" });
assert.equal(rejected.status, 1);
assert.match(rejected.stderr, /Increment.*IL_[0-9a-f]+.*add\.ovf/);
const oracle = readFileSync("artifacts/oracle.json");
const cases = JSON.parse(oracle);
console.log("Node:", await verify(name => readFileSync("artifacts/" + name + ".wasm"), cases,
    bytes => writeFileSync("artifacts/StandardWasm.Compiler.wasm", bytes)));

const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
});
try {
    const page = await browser.newPage();
    // Serve only these fixture assets to the isolated test page.
    const assets = new Map([
        // Serve the oracle as JSON so the browser can parse the DLL byte arrays
        // without Playwright serializing each individual byte as an argument.
        ["/oracle.json", ["application/json", oracle]],
        ["/bootstrap.mjs", ["text/javascript", readFileSync("scripts/bootstrap.mjs")]],
        ["/invoke.mjs", ["text/javascript", readFileSync("scripts/invoke.mjs")]],
        ["/verify.mjs", ["text/javascript", readFileSync("scripts/verify.mjs")]],
        ...demos.map(d => ["/" + d.assembly, ["application/wasm", readFileSync(d.wasm)]])
    ]);
    await page.route("http://standardwasm.test/**", async route => {
        const path = new URL(route.request().url()).pathname;
        const [contentType, body] = assets.get(path) || ["text/html", "<title>StandardWasm verification</title>"];
        await route.fulfill({ contentType, body });
    });
    await page.goto("http://standardwasm.test/");
    const result = await page.evaluate(async () => {
        const cases = await (await fetch("/oracle.json")).json();
        const { verify } = await import("/verify.mjs");
        return verify(async name => (await fetch("/" + name)).arrayBuffer(), cases);
    });
    console.log("Chromium " + browser.version() + ":", result);
} finally {
    await browser.close();
}
