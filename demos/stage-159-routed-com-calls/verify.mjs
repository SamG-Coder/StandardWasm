import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-159-routed-com-calls");
const evidence = join(root, "artifacts/stage-159-routed-com-calls");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
assert.equal(oracle.architecture, "X86");
const assembly = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, assembly, scalarRow, wrappedRow, instantiate) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const managed = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(managed, i, bytes[i]);
        return managed;
    }
    const pack = new Uint8Array(4 + assembly.length);
    new DataView(pack.buffer).setUint32(0, assembly.length, true);
    pack.set(assembly, 4);
    const entries = [];
    for (const [row, name] of [[scalarRow, "scalar"], [wrappedRow, "wrapped"]]) {
        const text = new TextEncoder().encode(name);
        const entry = new Uint8Array(8 + text.length);
        const words = new DataView(entry.buffer);
        words.setUint32(0, row, true);
        words.setUint32(4, text.length, true);
        entry.set(text, 8);
        entries.push(...entry);
    }
    const location = tool.CreateDiagnostic();
    const output = tool.CreateBytes(1 << 20);
    const length = tool.CompileBundleDetailed(upload(pack), upload(entries), output, location);
    if (length <= 0)
        throw Error(`Compiler status ${length} at ${[0, 1, 2].map(i => tool.ReadDiagnostic(location, i))}`);
    const wasm = Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i));
    const module = await WebAssembly.compile(wasm);
    const imports = WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind]);
    let site;
    let calls = 0;
    const instance = await instantiate(module, tool, [], {}, null, null, ({ registerInterface }) => {
        site = registerInterface("INotifySite", new Map([[3, {
            signature: "stdcall:i32(p32)",
            invoke: receiver => {
                if (receiver !== site) throw Error("COM callback lost its receiver");
                return calls++ === 0 ? 0 : -2147467259;
            }
        }]]));
    });
    const scalar = instance.exports.scalar(site);
    const wrapped = instance.exports.wrapped(site);
    return { scalar, wrapped, calls, imports };
}

const node = await exercise(compiler, assembly, oracle.scalarRow, oracle.wrappedRow, instantiateManagedModule);
assert.deepEqual(node, { scalar: oracle.scalar, wrapped: oracle.wrapped, calls: oracle.calls, imports: [
    ["standardwasm:resources", "memory", "memory"], ["standardwasm:com", "stdcall:i32(p32)", "function"]
] });
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, assembly, scalarRow, wrappedRow }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const response = await fetch("/StandardWasm.Compiler.wasm");
        if (!response.ok) throw Error("Could not load the frozen compiler");
        const compiler = await response.arrayBuffer();
        return (0, eval)("(" + source + ")")(compiler, new Uint8Array(assembly), scalarRow, wrappedRow, instantiateManagedModule);
    }, { source: exercise.toString(), assembly: [...assembly], scalarRow: oracle.scalarRow, wrappedRow: oracle.wrappedRow });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const results = { compiler: sha256(compiler), assembly: sha256(assembly), clr: oracle.runtime,
        clrArchitecture: oracle.architecture, node: process.version, edge: browser.version(), outcomes: node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2) + "\n");
    console.log(JSON.stringify(results));
} finally {
    await browser.close();
    await server.close();
}
