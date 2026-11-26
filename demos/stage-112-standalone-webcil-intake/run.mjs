import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { extname, join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { serveIntake } from "../stage-73-executable-intake/server.mjs";
import { inspectStandaloneEntry, openStandalonePublish } from "./loader/publish.mjs";

const stage = "demos/stage-112-standalone-webcil-intake";
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const compilerBytes = readFileSync(compiler);
const base = join(root, "artifacts/standalone-webcil");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const app = join(evidence, "App"), publish = join(evidence, "publish");
const name = "StandaloneBlazorReference";
const target = "net10.0";
run("dotnet", ["new", "blazorwasm", "-n", name, "-f", target, "--no-restore", "-o", app]);
run("dotnet", ["publish", app, "-c", "Release", "-o", publish]);
run("dotnet", ["build", stage + "/Inspector", "-c", "Release"]);

const webroot = join(publish, "wwwroot");
const stockFiles = visit(webroot);
const files = stockFiles.map(path => ({ path: "wwwroot/" + path, bytes: readFileSync(join(webroot, path)) }));
const native = files.find(asset => /^wwwroot\/_framework\/dotnet\.native\.[^.]+\.wasm$/.test(asset.path));
assert.ok(native);

const selection = await openStandalonePublish(files);
const linkedPe = join(app, "obj/Release", target, "linked", name + ".dll");
const inspector = join(root, stage, "Inspector/bin/Release/net10.0/Inspector.dll");
const reference = JSON.parse(execFileSync("dotnet", [inspector, linkedPe], { encoding: "utf8" }));
assert.equal(reference.assembly, name);
assert.equal(reference.entry, "Program::<Main>");
assert.equal(metadataHash(selection.entry.bytes), reference.metadataHash.toLowerCase());

await assert.rejects(openStandalonePublish(files.map(asset => asset === selection.entry.original
    ? { ...asset, bytes: native.bytes } : asset)), /Webcil/);
await assert.rejects(openStandalonePublish(files.filter(asset => asset !== selection.entry.original)), /original boot assembly/);
const changed = Uint8Array.from(selection.entry.original.bytes);
changed[changed.length - 1] ^= 1;
await assert.rejects(openStandalonePublish(files.map(asset => asset === selection.entry.original
    ? { ...asset, bytes: changed } : asset)), /digest mismatch/);

const node = await inspectStandaloneEntry(compilerBytes, selection);
assert.equal(node.entryToken, reference.token);
assert.ok(node.managedAssemblies > 1);
assert.equal(node.compilerStatus, -2);
// The constructor linker consumes a local address before diagnosing a non-constructor call.
// The original entry calls TaskAwaiter.GetResult at IL_000e.
assert.deepEqual(node.diagnostic, [0, reference.token & 0xffffff, 14]);
assert.equal(node.diagnosticAssembly, node.entry);

const pages = new Map();
const contentType = {
    ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".mjs": "text/javascript",
    ".json": "application/json", ".wasm": "application/wasm"
};
for (const path of stockFiles)
    pages.set("/" + path, [contentType[extname(path)] ?? "application/octet-stream", join(webroot, path)]);
pages.set("/", ["text/html", join(webroot, "index.html")]);
const prefix = "/" + stage;
pages.set(prefix + "/", ["text/html", join(root, stage, "web/index.html")]);
pages.set(prefix + "/compiler.wasm", ["application/wasm", compiler]);
for (const path of ["web/intake", "web/worker", "loader/publish", "loader/webcil"])
    pages.set(prefix + "/" + path + ".mjs", ["text/javascript", join(root, stage, path + ".mjs")]);
for (const path of ["stage-74-executable-entry/companions", "stage-73-executable-intake/loader/pe"])
    pages.set("/demos/" + path + ".mjs", ["text/javascript", join(root, "demos", path + ".mjs")]);

const server = await serveIntake(0, pages);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url + "/");
    await page.getByRole("link", { name: "Counter" }).click();
    const count = page.getByRole("status");
    assert.equal(await count.textContent(), "Current count: 0");
    await page.getByRole("button", { name: "Click me" }).click();
    assert.equal(await count.textContent(), "Current count: 1");

    await page.goto(server.url + prefix + "/");
    await page.locator("#folder").setInputFiles(publish);
    await page.waitForFunction(() => ["complete", "error"].includes(document.querySelector("#status").dataset.state),
        undefined, { timeout: 610000 });
    const edge = JSON.parse(await page.locator("#status").textContent());
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        sdk: execFileSync("dotnet", ["--version"], { encoding: "utf8" }).trim(),
        nodeVersion: process.version,
        browser: browser.version(),
        compilerHash: sha256(compilerBytes),
        originalEntryHash: sha256(selection.entry.original.bytes),
        stockCounter: [0, 1], reference, node, edge
    }, null, 2) + "\n");
    console.log("Stock Counter moved from 0 to 1. StandardWasm selected " + node.managedAssemblies +
        " original managed assets and matched across Node and Edge.");
    console.log(JSON.stringify(node));
    console.log("Evidence written to " + evidence);
} finally {
    await browser?.close();
    await server.close();
}

function visit(directory, prefix = "") {
    return readdirSync(directory, { withFileTypes: true }).flatMap(entry => entry.isDirectory()
        ? visit(join(directory, entry.name), prefix + entry.name + "/")
        : [prefix + entry.name]);
}

function sha256(bytes) {
    return createHash("sha256").update(bytes).digest("hex");
}

// Use the SDK's linked PE metadata as the independent byte-for-byte oracle.
function metadataHash(bytes) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const read = offset => view.getUint32(offset, true);
    const count = view.getUint16(8, true);
    function offsetFor(rva) {
        for (let index = 0; index < count; index++) {
            const header = 28 + 16 * index;
            const delta = rva - read(header + 4);
            if (delta >= 0 && delta < read(header + 8)) return read(header + 12) + delta;
        }
        throw Error("The SDK Webcil fixture has an unmapped RVA.");
    }
    const cli = offsetFor(read(12)), metadata = offsetFor(read(cli + 8));
    assert.equal(read(metadata), 0x424a5342);
    return sha256(bytes.subarray(metadata, metadata + read(cli + 12)));
}
