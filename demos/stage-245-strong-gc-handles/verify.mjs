import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos/stage-245-strong-gc-handles");
const evidence = join(root, "artifacts/stage-245-strong-gc-handles");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const assembly = readFileSync(oracle.assembly);
const bundle = Buffer.concat([assembly, ...oracle.dependencies.map(path => readFileSync(path))].flatMap(image => {
    const length = Buffer.alloc(4);
    length.writeUInt32LE(image.length);
    return [length, image];
}));
const assets = { "/compiler": compiler, "/bundle": bundle, "/root-probe": readFileSync(oracle.rootProbe) };
const scope = { names: Object.keys(oracle.normal), rows: oracle.rows, profiles: oracle.profiles };

async function verify(compiler, bundle, rootProbe, { names, rows, profiles }) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const output = tool.CreateBytes(2 << 20), diagnostic = tool.CreateDiagnostic();
    const encoder = new TextEncoder();
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(array, i, bytes[i]);
        return array;
    }
    const input = upload(new Uint8Array(bundle));
    function compile(exports) {
        const manifest = [];
        for (const name of exports) {
            const label = encoder.encode(name);
            const entry = new Uint8Array(8 + label.length), view = new DataView(entry.buffer);
            view.setUint32(0, rows[name], true);
            view.setUint32(4, label.length, true);
            entry.set(label, 8);
            manifest.push(...entry);
        }
        const status = tool.CompileBundleDetailed(input, upload(manifest), output, diagnostic);
        if (status <= 0) return { status, location: [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)) };
        return { bytes: Uint8Array.from({ length: status }, (_, i) => tool.ReadByte(output, i)) };
    }
    function moduleOf(bytes) {
        const module = new WebAssembly.Module(bytes);
        if (WebAssembly.Module.imports(module).length !== 0)
            throw Error("Unexpected host imports " + JSON.stringify(WebAssembly.Module.imports(module)));
        return module;
    }
    const compiled = compile([...names, "Unsupported", "HoldFinalizable", "Release", "ReadTarget", "ProbeTarget"]);
    if (!compiled.bytes) throw Error("Normal handle compile " + JSON.stringify(compiled));
    const module = moduleOf(compiled.bytes);
    const { exports: guest } = new WebAssembly.Instance(module);
    const { exports: isolated } = new WebAssembly.Instance(module);
    const normal = Object.fromEntries(names.map(name => [name, name === "InvalidToken" ? guest[name](0) : guest[name]()]));
    const unsupported = profiles.map(profile => guest.Unsupported(profile));
    const first = guest.HoldFinalizable(47);
    const independent = isolated.ProbeTarget(first);
    guest.Release(first);
    const second = guest.HoldFinalizable(59);
    const stale = guest.ProbeTarget(first), replacement = guest.ReadTarget(second);
    guest.Release(second);

    const { exports: traced } = new WebAssembly.Instance(moduleOf(rootProbe));
    const tracedToken = traced.HoldFinalizable(53);
    const during = traced.__root_probe_discover();
    const held = traced.Finalized(), value = traced.ReadTarget(tracedToken);
    traced.Release(tracedToken);
    const after = traced.__root_probe_discover();
    const ran = traced.__root_probe_run_next();
    const released = traced.Finalized();
    const pinnedAddress = compile(["PinnedAddress"]);
    return {
        normal, unsupported, independent, stale, replacement, differentTokens: first !== second,
        roots: { held, value, released }, finalizerQueue: { during, after, ran },
        pinnedAddress: { status: pinnedAddress.status, location: pinnedAddress.location }
    };
}

const node = await verify(...Object.values(assets), scope);
assert.deepEqual(node.normal, oracle.normal);
assert.deepEqual(oracle.windowsProfiles, [0, 0, 0]);
assert.deepEqual(node.unsupported, [101, 101, 101]);
assert.deepEqual([node.independent, node.stale, node.replacement, node.differentTokens], [109, 109, 59, true]);
assert.deepEqual(node.roots, oracle.roots);
assert.deepEqual(node.finalizerQueue, { during: 0, after: 1, ran: 1 });
assert.equal(node.pinnedAddress.status, -2);
assert.deepEqual(node.pinnedAddress.location.slice(0, 2), [1, oracle.pinnedAddressRow]);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const bytes = assets[new URL(route.request().url()).pathname];
        return bytes ? route.fulfill({ body: bytes, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, paths, scope }) => {
        const inputs = await Promise.all(paths.map(async name => (await fetch(name)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...inputs, scope);
    }, { source: verify.toString(), paths: Object.keys(assets), scope });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(assembly), rootProbe: sha256(assets["/root-probe"]),
        clr: oracle.runtime, nodeVersion: process.version, edgeVersion: browser.version(),
        oracle: { normal: oracle.normal, roots: oracle.roots, profiles: oracle.windowsProfiles }, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 normal GCHandle lifecycle and strong finalization roots.");
} finally {
    await browser.close();
}
