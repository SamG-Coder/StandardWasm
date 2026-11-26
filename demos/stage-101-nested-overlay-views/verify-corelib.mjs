import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = "demos/stage-101-nested-overlay-views";
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const base = join(root, "artifacts", "corelib-overlay");
mkdirSync(base, { recursive: true });
const evidence = mkdtempSync(join(base, "run-"));
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "-c", "Release", "--project", stage + "/Oracle", "--", oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const routes = ["/compiler", "/app", "/facade", "/core"];
const files = [compiler, oracle.app, oracle.facade, oracle.core].map(path => readFileSync(path));

async function check(compiler, images, entry, inputs) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const parts = images.map(image => new Uint8Array(image));
    const bytes = new Uint8Array(parts.reduce((length, part) => length + 4 + part.length, 0));
    const sizes = new DataView(bytes.buffer);
    let start = 0;
    for (const part of parts) {
        sizes.setUint32(start, part.length, true);
        bytes.set(part, start + 4);
        start += 4 + part.length;
    }
    const manifest = new Uint8Array(11);
    new DataView(manifest.buffer).setUint32(0, entry, true);
    manifest.set([3, 0, 0, 0, 114, 117, 110], 4);
    function upload(source) {
        const target = build.CreateBytes(source.length);
        for (let i = 0; i < source.length; i++) build.SetByte(target, i, source[i]);
        return target;
    }
    const capacity = 65536;
    const output = build.CreateBytes(capacity);
    const diagnostic = build.CreateDiagnostic();
    const status = build.CompileBundleDetailed(upload(bytes), upload(manifest), output, diagnostic);
    if (status < 1 || status > capacity)
        throw Error("Compiler returned " + status + " at " + [0, 1, 2].map(i => build.ReadDiagnostic(diagnostic, i)));
    const code = Uint8Array.from({ length: status }, (_, index) => build.ReadByte(output, index));
    const { instance } = await WebAssembly.instantiate(code);
    return { bytes: Array.from(code), actual: inputs.map(input => instance.exports.run(BigInt(input)).toString()) };
}

const node = await check(files[0], files.slice(1), oracle.entry, oracle.inputs);
assert.deepEqual(node.actual, oracle.expected);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const index = routes.indexOf(new URL(route.request().url()).pathname);
        return index < 0 ? route.fulfill({ contentType: "text/html", body: "<!doctype html>" }) :
            route.fulfill({ contentType: "application/octet-stream", body: files[index] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ routes, entry, inputs, source }) => {
        const [compiler, ...images] = await Promise.all(routes.map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, images, entry, inputs);
    }, { routes, entry: oracle.entry, inputs: oracle.inputs, source: check.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "overlay.wasm"), Uint8Array.from(node.bytes));
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, node: process.version, edge: browser.version(), results: node.actual, bytes: node.bytes.length
    }, null, 2) + "\n");
    console.log("Node and Edge matched " + node.actual.length + " installed CoreLib DateTime overlays on " + oracle.runtime + ".");
} finally {
    await browser.close();
}
