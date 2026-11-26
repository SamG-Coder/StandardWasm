import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { verifyNullable } from "./probe-corelib.mjs";

const stage = "demos/stage-98-nonnullable-value-constraints";
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const base = join(root, "artifacts", "corelib-nullable");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "-c", "Release", "--project", stage + "/Oracle", "--", oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const paths = ["/compiler", "/application", "/invalid", "/facade", "/core"];
const files = new Map(paths.map((path, index) => [path, [
    "application/octet-stream", readFileSync([compiler, oracle.app, oracle.invalid, oracle.facade, oracle.core][index])
]]));
files.set("/probe.mjs", ["text/javascript", readFileSync(join(root, stage, "probe-corelib.mjs"))]);
const node = await verifyNullable(...paths.map(path => files.get(path)[1]), oracle);

const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const [contentType, body] = files.get(new URL(route.request().url()).pathname) ??
            ["text/html", "<title>Installed CoreLib Nullable</title>"];
        return route.fulfill({ contentType, body });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ paths, oracle }) => {
        const { verifyNullable } = await import("/probe.mjs");
        const images = await Promise.all(paths.map(async path => (await fetch(path)).arrayBuffer()));
        return verifyNullable(...images, oracle);
    }, { paths, oracle });
    assert.deepEqual(edge, node);

    const { bytes, ...results } = node;
    const wasm = join(evidence, "nullable.wasm"), wat = join(evidence, "nullable.wat");
    writeFileSync(wasm, Uint8Array.from(bytes));
    run(process.execPath, ["node_modules/binaryen/bin/wasm-dis", wasm, "-o", wat]);
    const layout = readFileSync(wat, "utf8");
    assert.match(layout, /\(struct \(field i8\) \(field i8\)\)/);
    assert.match(layout, /\(struct \(field i8\) \(field i32\)\)/);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        node: process.version, edge: browser.version(), runtime: oracle.runtime, coreIdentity: oracle.coreIdentity,
        ...results, bytes: bytes.length
    }, null, 2) + "\n");
    console.log("Node and Edge matched the installed CLR Nullable field copies and rejected the nested argument.");
} finally {
    await browser.close();
}
