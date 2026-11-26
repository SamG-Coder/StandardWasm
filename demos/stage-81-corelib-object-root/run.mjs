import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { verifyCoreRoot } from "./probe.mjs";

const stage = "demos/stage-81-corelib-object-root";
const base = join(root, "artifacts", "corelib-object-root");
mkdirSync(base, { recursive: true });
const dir = mkdtempSync(join(base, "run-"));
const oracleFile = join(dir, "oracle.json");
const compilerPath = join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compilerPath)) throw Error("Build the self-hosted compiler with npm test first.");
run("dotnet", ["run", "-c", "Release", "--project", stage + "/Oracle", "--", oracleFile]);
const oracle = JSON.parse(readFileSync(oracleFile, "utf8"));
const files = new Map([
    ["/verify.mjs", ["text/javascript", readFileSync(join(root, stage, "probe.mjs"))]],
    ["/compiler.wasm", ["application/wasm", readFileSync(compilerPath)]],
    ["/app", ["application/octet-stream", readFileSync(oracle.app)]],
    ["/facade", ["application/octet-stream", readFileSync(oracle.facade)]],
    ["/runtime", ["application/octet-stream", readFileSync(oracle.runtimeFacade)]],
    ["/core", ["application/octet-stream", readFileSync(oracle.core)]]
]);
const inputs = ["/compiler.wasm", "/app", "/facade", "/runtime", "/core"];
const node = await verifyCoreRoot(...inputs.map(path => files.get(path)[1]), oracle);
const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
});
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", async route => {
        const [contentType, body] = files.get(new URL(route.request().url()).pathname) ??
            ["text/html", "<title>CoreLib reference verification</title>"];
        await route.fulfill({ contentType, body });
    });
    await page.goto("http://127.0.0.1/");
    const browserResult = await page.evaluate(async ({ inputs, fixture }) => {
        const { verifyCoreRoot } = await import("/verify.mjs");
        const bytes = await Promise.all(inputs.map(async path => new Uint8Array(await (await fetch(path)).arrayBuffer())));
        return verifyCoreRoot(...bytes, fixture);
    }, { inputs, fixture: { method: oracle.method, arguments: oracle.arguments, expected: oracle.expected } });
    assert.deepEqual(browserResult, node);
    writeFileSync(join(dir, "results.json"), JSON.stringify({
        node: process.version, browser: browser.version(), runtime: oracle.runtime,
        coreIdentity: oracle.coreIdentity, result: node
    }, null, 2) + "\n");
    console.log("Node and Edge matched the CLR with the installed CoreLib constructor chain.");
} finally {
    await browser.close();
}
