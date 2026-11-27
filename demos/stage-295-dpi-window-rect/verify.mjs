import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-295-dpi-window-rect");
const evidence = join(root, "artifacts/stage-295-dpi-window-rect");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, image, cases, instantiate) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const source = upload(image), encode = name => upload(new TextEncoder().encode(name));
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const entries = [];
    for (const name of ["Adjust", "ExecutableModule"]) {
        const encoded = new TextEncoder().encode(name);
        const row = tool.FindRow(source, upload(encoded));
        if (row < 1) throw Error("Missing export " + name);
        const entry = new Uint8Array(8 + encoded.length);
        const view = new DataView(entry.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, encoded.length, true);
        entry.set(encoded, 8);
        entries.push(...entry);
    }
    const size = tool.CompileModuleDetailed(source, upload(entries), output, diagnostic);
    const location = () => [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i));
    if (size < 1) throw Error("Fixture compilation " + size + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module).map(item => item.module + "/" + item.name).sort();
    const expectedImports = ["standardwasm:resources/memory", "standardwasm:win32/adjustWindowRectExForDpi",
        "standardwasm:win32/getModuleHandleW"];
    if (imports.join() !== expectedImports.join()) throw Error("Unexpected imports " + imports);

    const wrong = tool.FindRow(source, encode("RejectWrongPointee"));
    const wrongMethod = tool.FindRow(source, encode("WrongPointee"));
    const rejection = { status: tool.CompileDetailed(source, wrong, output, diagnostic), location: location() };
    if (rejection.status !== -2 || rejection.location.join() !== [0, wrongMethod, -1].join())
        throw Error("The non-RECT pointee was not diagnosed at the import");

    let memory, errors;
    const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
        configureNativeLibraries(registry) { ({ memory, errors } = registry); }
    });
    if (!guest.ExecutableModule()) throw Error("Missing executable module");
    const address = memory.grow(1) * 65536;
    const view = new DataView(memory.buffer);
    const read = () => [0, 4, 8, 12].map(offset => view.getInt32(address + offset, true));
    const results = cases.map(({ style, extendedStyle, menu, dpi, before }) => {
        for (let i = 0; i < before.length; i++) view.setInt32(address + 4 * i, before[i], true);
        return { result: guest.Adjust(address, style, menu, extendedStyle, dpi), after: read() };
    });
    const failure = pointer => ({ result: guest.Adjust(pointer, 0, 0, 0, 96), error: errors.system });
    const badAddresses = [failure(0), failure(memory.buffer.byteLength - 15)];
    const beforeInvalidDpi = read();
    const invalidDpi = { result: guest.Adjust(address, 0, 0, 0, 0), error: errors.system,
        unchanged: beforeInvalidDpi.join() === read().join() };
    return { results, imports, rejection, badAddresses, invalidDpi };
}

const expected = oracle.cases.map(({ result, after }) => ({ result, after }));
const node = await exercise(compiler, image, oracle.cases, instantiateManagedModule);
assert.equal(node.results.length, expected.length);
for (let i = 0; i < expected.length; i++) {
    const { dpi, style, extendedStyle, menu } = oracle.cases[i];
    assert.deepEqual(node.results[i], expected[i],
        `Windows parity at DPI ${dpi}, style 0x${(style >>> 0).toString(16)}, extended style 0x${extendedStyle.toString(16)}, menu ${menu}`);
}
assert.deepEqual(node.badAddresses, Array(2).fill({ result: 0, error: 998 }));
assert.deepEqual(node.invalidDpi, { result: 0, error: 87, unchanged: true });

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/fixture.dll", route => route.fulfill({ body: image, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ cases, code }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture.dll"]
            .map(async path => {
                const response = await fetch(path);
                if (!response.ok) throw Error(path + " returned HTTP " + response.status);
                return response.arrayBuffer();
            }));
        return (0, eval)("(" + code + ")")(...assets.map(bytes => new Uint8Array(bytes)), cases, instantiateManagedModule);
    }, { cases: oracle.cases, code: exercise.toString() });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const evidenceRecord = { compiler: sha256(compiler), assembly: sha256(image), clr: oracle.runtime,
        node: process.version, edge: browser.version(), cases: oracle.cases.length, imports: node.imports,
        rejection: node.rejection, badAddresses: node.badAddresses, invalidDpi: node.invalidDpi };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(evidenceRecord, null, 2) + "\n");
    console.log(JSON.stringify(evidenceRecord));
} finally {
    await browser?.close();
    await server.close();
}
