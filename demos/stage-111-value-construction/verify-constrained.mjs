import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { compile, root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

const stage = join(root, "demos/stage-111-value-construction");
const artifacts = join(root, "artifacts", "constrained-value-calls");
mkdirSync(artifacts, { recursive: true });
const evidence = mkdtempSync(join(artifacts, "run-"));
const runtime = wasm32Runtime();
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));

const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(evidence, "compiler.wasm");
if (!process.env.STANDARDWASM_COMPILER_PATH) {
    run("dotnet", ["build", "-c", "Release", "demos/stage-13-complete-bootstrap"]);
    compile(join(root, "demos/stage-13-complete-bootstrap/bin/Release/netstandard2.0/BootstrapLibrary.dll"),
        "BootstrapLibrary.CompilerExports", compiler);
}
const fixturePath = join(stage, "bin/Release/netstandard2.0");
const images = [join(fixturePath, "ValueConstructionDemo.dll"), join(fixturePath, "ValueConstructionDependency.dll"),
    ...["netstandard.dll", "System.Runtime.dll", "System.Private.CoreLib.dll"].map(file => join(runtime.directory, file))]
    .map(file => readFileSync(file));
const bundle = Buffer.concat(images.flatMap(image => {
    const length = Buffer.alloc(4);
    length.writeUInt32LE(image.length);
    return [length, image];
}));

async function verify(compiler, fixture, packed, inputs) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const result = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(result, i, bytes[i]);
        return result;
    }
    const image = upload(fixture), bundle = upload(packed);
    function compileEntry(name) {
        const row = tool.FindRow(image, upload(new TextEncoder().encode(name)));
        if (row <= 0) throw Error("Missing fixture " + name);
        const manifest = new Uint8Array(11);
        const header = new DataView(manifest.buffer);
        header.setUint32(0, row, true);
        header.setUint32(4, 3, true);
        manifest.set([114, 117, 110], 8);
        const output = tool.CreateBytes(1 << 20), location = tool.CreateDiagnostic();
        const status = tool.CompileBundleDetailed(bundle, upload(manifest), output, location);
        if (status <= 0) return { status, diagnostic: [0, 1, 2].map(i => tool.ReadDiagnostic(location, i)) };
        return { bytes: Uint8Array.from({ length: status }, (_, i) => tool.ReadByte(output, i)) };
    }
    async function runEntry(name, imports = {}, invocations = inputs) {
        const supported = compileEntry(name);
        if (!supported.bytes) throw Error(JSON.stringify({ name, ...supported }));
        const { instance } = await WebAssembly.instantiate(supported.bytes, imports);
        return invocations.map(instance.exports.run);
    }
    const debuggerImports = { "standardwasm:runtime": { isDebuggerAttached: () => 0 } };
    const [debuggerStatus] = await runEntry("ReadManagedDebuggerStatus", debuggerImports, [undefined]);
    return { values: await runEntry("InvokeValueOverride"), typeBranches: await runEntry("CheckTypeBranches"), debuggerStatus,
        closedClasses: await runEntry("CheckClosedClassSlots"), closedClassNull: await runEntry("CheckClosedClassNull"),
        closedInterfaces: await runEntry("CheckClosedInterface"), closedInterfaceNull: await runEntry("CheckClosedInterfaceNull"),
        primitiveHashes: await runEntry("CheckPrimitiveHash"),
        primitiveDescriptors: await runEntry("CheckPrimitiveDescriptors"),
        closedValueTypes: await runEntry("CheckClosedValueType"),
        entryAliases: await runEntry("UpdateEntryArray"), entryFailures: await runEntry("CheckEntryArrayFailure"),
        bucketAliases: await runEntry("ReadManagedBuckets"), bucketFailures: await runEntry("CheckBucketFailure"),
        bucketExportRejection: compileEntry("RejectExportedBucket"), rejection: compileEntry("RejectInheritedValueHash") };
}

const assets = { "/compiler": readFileSync(compiler), "/fixture": images[0], "/bundle": bundle };
const expected = {
    values: oracle.values,
    typeBranches: oracle.typeBranches,
    debuggerStatus: oracle.debuggerStatus,
    closedClasses: oracle.closedClasses,
    closedClassNull: oracle.closedClassNull,
    closedInterfaces: oracle.closedInterfaces,
    closedInterfaceNull: oracle.closedInterfaceNull,
    primitiveHashes: oracle.primitiveHashes,
    primitiveDescriptors: oracle.primitiveDescriptors,
    closedValueTypes: oracle.closedValueTypes,
    entryAliases: oracle.entryAliases,
    entryFailures: oracle.entryFailures,
    bucketAliases: oracle.bucketAliases,
    bucketFailures: oracle.bucketFailures,
    bucketExportRejection: { status: -2, diagnostic: oracle.bucketExportDiagnostic },
    rejection: { status: -2, diagnostic: oracle.boxingDiagnostic }
};
const node = await verify(...Object.values(assets), oracle.inputs);
assert.deepEqual(node, expected);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        return path === "/" ? route.fulfill({ contentType: "text/html", body: "<!doctype html>" }) :
            route.fulfill({ contentType: "application/octet-stream", body: assets[path] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, paths, inputs }) => {
        const bytes = await Promise.all(paths.map(async path => new Uint8Array(await (await fetch(path)).arrayBuffer())));
        return (0, eval)("(" + source + ")")(...bytes, inputs);
    }, { source: verify.toString(), paths: Object.keys(assets), inputs: oracle.inputs });
    assert.deepEqual(edge, expected);
    const result = JSON.stringify({ runtime: runtime.version, nodeVersion: process.version, edgeVersion: browser.version(), node, edge });
    writeFileSync(join(evidence, "results.json"), result + "\n");
    console.log(result);
} finally {
    await browser.close();
}
