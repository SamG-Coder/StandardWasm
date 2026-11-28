import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-205-register-window-message");
const evidence = join(root, "artifacts/stage-205-register-window-message");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
const runtime = wasm32Runtime();
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/RegisteredWindowMessages.dll"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));

async function exercise({ compiler, image, oracle }, instantiate) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const data = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(data, i, bytes[i]);
        return data;
    }
    const source = upload(decode(image));
    const encoder = new TextEncoder();
    const entries = [];
    for (const name of ["ExecutableModule", "Register", "Error", "SetErrors", "SavedError"]) {
        const encoded = encoder.encode(name);
        const row = tool.FindRow(source, upload(encoded));
        if (row < 1) throw Error("Missing export " + name);
        const entry = new Uint8Array(8 + encoded.length), view = new DataView(entry.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, encoded.length, true);
        entry.set(encoded, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), location = tool.CreateDiagnostic();
    const count = tool.CompileModuleDetailed(source, upload(entries), output, location);
    if (count < 1) throw Error("Fixture compilation " + count + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(location, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: count }, (_, i) => tool.ReadByte(output, i)));
    if (!WebAssembly.Module.imports(module).some(entry =>
        entry.module === "standardwasm:win32" && entry.name === "registerWindowMessageW"))
        throw Error("The compiler did not bind RegisterWindowMessageW");

    async function launch() {
        let memory;
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (!guest.ExecutableModule()) throw Error("Missing executable module");
        const address = memory.grow(1) * 65536;
        const view = new DataView(memory.buffer);

        function register(pointer, captureError) {
            guest.SetErrors(oracle.system, oracle.saved);
            const id = guest.Register(pointer, captureError);
            return { id, error: guest.Error(), saved: guest.SavedError() };
        }

        function probe({ text, oddAddress, captureError }) {
            if (text === null) return register(0, captureError);
            const pointer = address + Number(oddAddress);
            for (let i = 0; i < text.length; i++) view.setUint16(pointer + i * 2, text.charCodeAt(i), true);
            view.setUint16(pointer + text.length * 2, 0, true);
            return register(pointer, captureError);
        }
        return { probe, register, view };
    }

    const { probe, register, view } = await launch();
    const cases = oracle.cases.map(({ input }) => probe(input));
    view.setUint16(view.byteLength - 2, 65, true);
    const badPointer = register(view.byteLength - 2, false);
    const other = await launch();
    const secondLaunchFirst = other.probe(oracle.cases[4].input).id;
    return { cases, badPointer, secondLaunchFirst };
}

function normalize(outcomes) {
    const ids = new Map();
    return outcomes.map(({ id, ...errors }) => {
        if (id < 0xc000) return { id, ...errors };
        assert.ok(id <= 0xffff);
        if (!ids.has(id)) ids.set(id, ids.size);
        return { nameIndex: ids.get(id), ...errors };
    });
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64"), oracle };
const node = await exercise(input, instantiateManagedModule);
assert.ok(node.cases[0].id >= 0xc000 && node.cases[4].id >= 0xc000);
assert.notEqual(node.cases[0].id, node.cases[4].id);
assert.deepEqual(normalize(node.cases), normalize(oracle.cases.map(test => test.output)));
assert.deepEqual(node.badPointer, { id: 0, error: 998, saved: oracle.saved });
assert.equal(node.secondLaunchFirst, node.cases[0].id);

const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, code }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + code + ")")(input, instantiateManagedModule);
    }, { input, code: exercise.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"), clr: runtime.version,
        nodeVersion: process.version, edgeVersion: browser.version(), oracle, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 across " + node.cases.length + " registered window message cases.");
} finally {
    await browser?.close();
    await server.close();
}
