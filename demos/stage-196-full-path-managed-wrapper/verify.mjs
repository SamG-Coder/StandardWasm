import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-196-full-path-managed-wrapper");
const evidence = join(root, "artifacts/stage-196-full-path-managed-wrapper");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/FullPathManagedWrapper.dll"));

async function verify({ compiler, image, names }, instantiate) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const input = upload(decode(image));
    const output = tool.CreateBytes(1 << 18);
    const diagnostic = tool.CreateDiagnostic();
    const results = {};
    for (const name of names) {
        const row = tool.FindRow(input, upload(new TextEncoder().encode(name)));
        if (row < 1) throw Error("Missing fixture export " + name);
        const length = tool.CompileDetailed(input, row, output, diagnostic);
        if (length < 1)
            throw Error(name + " status " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
        const imports = WebAssembly.Module.imports(module);
        if (!imports.some(entry => entry.name === "getFullPathNameW") ||
            !imports.some(entry => entry.name === "setLastPInvokeError"))
            throw Error("Missing scoped Win32 imports for " + name);
        const { exports } = await instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
            win32FileSystem: { currentDirectory: "R:\\virtual\\work" }
        });
        results[name] = exports.run();
    }
    return results;
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64"), names: Object.keys(oracle.results) };
const node = await verify(input, instantiateManagedModule);
assert.deepEqual(node, oracle.results);
assert.ok(Object.values(node).every(value => value === 1));
const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"),
        clr: oracle.clr, nodeVersion: process.version, edgeVersion: browser.version(), oracle: oracle.results, node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched the Windows x86 CLR on all managed full-path wrapper cases.");
} finally {
    await browser?.close();
    await server.close();
}
