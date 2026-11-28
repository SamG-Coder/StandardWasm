import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-315-thread-message-queue");
const evidence = join(root, "artifacts/stage-315-thread-message-queue");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const oracleProject = join(stage, "Oracle");
runOn32BitClr(wasm32Runtime(), oracleProject, join(oracleProject, "bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const fixture = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, fixture, operations, instantiate) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const source = upload(fixture);
    const encoder = new TextEncoder();
    const find = name => tool.FindRow(source, upload(encoder.encode(name)));
    const manifest = [];
    for (const name of ["ThreadId", "ExecutableModule", "SavedError", "SetSavedError", "Post", "Peek"]) {
        const row = find(name);
        if (row < 1) throw Error("Missing export " + name);
        const bytes = encoder.encode(name);
        const record = new Uint8Array(8 + bytes.length);
        const view = new DataView(record.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, bytes.length, true);
        record.set(bytes, 8);
        manifest.push(...record);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const size = tool.CompileModuleDetailed(source, upload(manifest), output, diagnostic);
    if (size < 1) throw Error("Fixture compilation " + size + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, index) => tool.ReadByte(output, index)));
    const imports = WebAssembly.Module.imports(module).map(item => item.module + "/" + item.name).sort();
    for (const member of ["postThreadMessageW", "peekMessageW"]) {
        if (imports.filter(name => name.endsWith("/" + member)).join() !== "standardwasm:win32/" + member)
            throw Error("Expected one shared host import for " + member);
    }
    const status = tool.CompileDetailed(source, find("RejectWrongPointee"), output, diagnostic);
    const rejection = { status, location: location() };
    if (status !== -2 || rejection.location.join() !== [0, find("WrongPointee"), -1].join())
        throw Error("The non-MSG pointee was not diagnosed");

    async function launch() {
        let memory;
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (!guest.ExecutableModule()) throw Error("Missing executable module");
        const address = memory.grow(1) * 65536;
        const view = new DataView(memory.buffer);
        function peek(window, minimum, maximum, flags) {
            const read = index => view.getInt32(address + index * 4, true);
            for (let index = 0; index < 8; index++) view.setInt32(address + index * 4, -1, true);
            const result = guest.Peek(address, window, minimum, maximum, flags);
            return { result, hwnd: read(0), message: read(1) >>> 0, wParam: read(2) >>> 0, lParam: read(3),
                timestampWritten: read(4) !== -1, pointWritten: read(5) !== -1 && read(6) !== -1, guard: read(7) };
        }
        return { guest, peek, memory };
    }

    const { guest, peek, memory } = await launch();
    const thread = guest.ThreadId();
    const results = operations.map(operation => {
        if (operation.kind === "peek")
            return peek(operation.window, operation.minimum, operation.maximum, operation.flags);
        guest.SetSavedError(789);
        const result = guest.Post(operation.current ? thread : -1, operation.message, operation.wParam,
            operation.lParam, operation.capture);
        return { result, error: guest.SavedError() };
    });
    guest.Post(thread, 0x8049, 51, -52, 1);
    const sibling = await launch();
    const isolated = sibling.peek(-1, 0x8049, 0x8049, 1).result === 0;
    const badPointers = [0, memory.buffer.byteLength - 27].map(pointer => guest.Peek(pointer, -1, 0, 0, 1));
    const retained = peek(-1, 0x8049, 0x8049, 1);
    return { results, imports, rejection, isolated, badPointers, retained };
}

const node = await exercise(compiler, fixture, oracle.operations, instantiateManagedModule);
for (const [index, operation] of oracle.operations.entries())
    assert.deepEqual(node.results[index], operation.expected, `Windows parity for operation ${index} (${operation.kind})`);
assert.equal(node.isolated, true);
assert.deepEqual(node.badPointers, [0, 0]);
assert.equal(node.retained.wParam, 51);
assert.equal(node.retained.lParam, -52);

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/fixture.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ operations, code }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/fixture.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + code + ")")(...assets, operations, instantiateManagedModule);
    }, { operations: oracle.operations, code: exercise.toString() });
    assert.deepEqual(edge, node);

    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const result = { compiler: sha256(compiler), assembly: sha256(fixture), clr: oracle.runtime,
        node: process.version, edge: browser.version(), comparisons: oracle.operations.length, ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log(JSON.stringify({ comparisons: result.comparisons, clr: result.clr, node: result.node, edge: result.edge }));
} finally {
    await browser?.close();
    await server.close();
}
