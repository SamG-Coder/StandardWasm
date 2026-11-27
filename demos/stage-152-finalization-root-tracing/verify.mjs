import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-152-finalization-root-tracing");
const artifacts = join(root, "artifacts", "finalization-root-tracing");
mkdirSync(artifacts, { recursive: true });
const evidence = mkdtempSync(join(artifacts, "run-"));
const oraclePath = join(evidence, "oracle.json");
run("dotnet", ["run", "-c", "Release", "--project", join(stage, "Oracle"), "--", oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const assets = {
    compiler: readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm")),
    fixture: readFileSync(join(stage, "Fixture", "bin", "Release", "netstandard2.0", "Fixture.dll")),
    rootProbe: readFileSync(join(evidence, "root-probe.wasm")),
    row: oracle.row,
    count: oracle.result.length
};

async function probe({ compiler, fixture, row, count }) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const data = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(data, i, bytes[i]);
        return data;
    }
    const bundle = new Uint8Array(fixture.length + 4);
    new DataView(bundle.buffer).setUint32(0, fixture.length, true);
    bundle.set(fixture, 4);
    const entries = new Uint8Array(11);
    new DataView(entries.buffer).setUint32(0, row, true);
    entries[4] = 3;
    entries.set([114, 117, 110], 8);
    const bytes = tool.CreateBytes(1 << 20);
    const diagnostic = tool.CreateDiagnostic();
    const size = tool.CompileBundleDetailed(upload(bundle), upload(entries), bytes, diagnostic);
    if (size < 1) throw new Error("Compiler status " + size + " at " +
        [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = await WebAssembly.compile(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(bytes, i)));
    if (WebAssembly.Module.imports(module).length !== 0) throw new Error("The experiment must run without host imports");
    if (WebAssembly.Module.exports(module).some(entry => entry.name.startsWith("__root_probe_")))
        throw new Error("Ordinary compilation exposed the diagnostic root stack");
    const instance = await WebAssembly.instantiate(module);
    return Array.from({ length: count }, (_, i) => instance.exports.run(i));
}

async function observeRoots(bytes) {
    const snapshots = [];
    let published;
    let inspectGraph = false;
    let reachable;
    const { instance } = await WebAssembly.instantiate(bytes, {
        "standardwasm:console": {
            write() { throw new Error("The probe emits only line breaks"); },
            newLine() {
                const { __root_probe_count: count, __root_probe_table: table } = instance.exports;
                const roots = Array.from({ length: count.value }, (_, index) => table.get(index));
                if (roots.some(value => value === null)) throw new Error("Expected a live object in every snapshot slot");
                published = roots;
                if (inspectGraph) reachable = instance.exports.__root_probe_mark();
                snapshots.push({ count: roots.length, unique: new Set(roots).size });
            }
        }
    });
    const { __root_probe_table: table, __root_probe_count: count, __root_probe_edge_count: edges,
        __root_probe_edge_read: read, __root_probe_mark: mark, __root_probe_contains: contains,
        __root_probe_clear: clear, ObserveFrames, ObserveConstructor, ThrowWithRoot, BuildGraph, SetStaticRoot } = instance.exports;
    function assertEmpty() {
        if (count.value !== 0) throw new Error("Root scope was not restored");
        for (let i = 0; i < table.length; i++)
            if (table.get(i) !== null) throw new Error("Root slot was not cleared");
    }
    const value = ObserveFrames(11);
    assertEmpty();
    const constructed = ObserveConstructor(11);
    assertEmpty();
    let exception;
    try { ThrowWithRoot(11); }
    catch (error) { exception = error; }
    if (!(exception instanceof WebAssembly.Exception) || !exception.is(instance.exports["\0nullReference"]))
        throw new Error("Expected the managed NullReferenceException tag");
    assertEmpty();
    if (ObserveFrames(11) !== value) throw new Error("Failed to reuse the module after unwinding");
    assertEmpty();
    inspectGraph = true;
    const root = BuildGraph(11);
    inspectGraph = false;
    assertEmpty();
    if (published.length !== 1 || published[0] !== root)
        throw new Error("The graph root was missing during the call");
    const inherited = read(root, 0);
    const next = read(root, 1);
    const array = read(root, 2);
    const payload = read(root, 4);
    if (edges(null) !== 0 || read(null, 0) !== null || read(array, -1) !== null ||
        read(array, edges(array)) !== null || read(root, edges(root)) !== null ||
        payload === null || edges(payload) !== 0 || read(payload, 0) !== null)
        throw new Error("Unexpected edge read for a missing reference or a payload without references");
    if ([root, inherited, next, array, payload].some(item => contains(item) !== 1) || contains(null) !== 0)
        throw new Error("The marked snapshot lost a node or included null");
    clear();
    if (contains(root) !== 0 || mark() !== 0) throw new Error("A released frame still appears in the mark set");
    if (SetStaticRoot(root) !== null || mark() !== reachable || contains(next) !== 1)
        throw new Error("The static root did not keep its graph reachable");
    if (SetStaticRoot(null) !== root || mark() !== 0 || contains(next) !== 0)
        throw new Error("The removed static still appears in the mark set");
    clear();
    const graph = { fields: edges(root), arrayLength: edges(array), inherited: inherited !== null,
        cycle: read(next, 1) === root, arrayAliases: read(array, 0) === next && read(array, 2) === root,
        nullEdges: Number(read(array, 1) === null) + Number(read(root, 3) === null), reachable };
    return { values: [value, constructed], snapshots, clearedOnReturn: true, clearedOnUnwind: true, graph, staticRoot: true };
}

const expectedRoots = { values: oracle.rootProbeResult,
    snapshots: [{ count: 3, unique: 2 }, { count: 2, unique: 1 }, { count: 1, unique: 1 }, { count: 3, unique: 2 }, { count: 1, unique: 1 }],
    clearedOnReturn: true, clearedOnUnwind: true, graph: oracle.graph, staticRoot: true };
const node = await probe(assets);
assert.deepEqual(node, oracle.result);
const nodeRoots = await observeRoots(assets.rootProbe);
assert.deepEqual(nodeRoots, expectedRoots);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const name = new URL(route.request().url()).pathname.slice(1);
        return name ? route.fulfill({ body: assets[name], contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, row, count }) => {
        const compiler = new Uint8Array(await (await fetch("/compiler")).arrayBuffer());
        const fixture = new Uint8Array(await (await fetch("/fixture")).arrayBuffer());
        return (0, eval)("(" + source + ")")({ compiler, fixture, row, count });
    }, { source: probe.toString(), row: assets.row, count: assets.count });
    assert.deepEqual(edge, oracle.result);
    const edgeRoots = await page.evaluate(async source => {
        const bytes = await (await fetch("/rootProbe")).arrayBuffer();
        return (0, eval)("(" + source + ")")(bytes);
    }, observeRoots.toString());
    assert.deepEqual(edgeRoots, expectedRoots);
    const result = { nodeVersion: process.version, edgeVersion: browser.version(), oracle, node, edge, nodeRoots, edgeRoots,
        compilerSha256: createHash("sha256").update(assets.compiler).digest("hex"),
        fixtureSha256: createHash("sha256").update(assets.fixture).digest("hex"),
        rootProbeSha256: createHash("sha256").update(assets.rootProbe).digest("hex") };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log(JSON.stringify(result));
} finally {
    await browser.close();
}
