import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos/stage-190-window-creation-binding");
const evidence = join(root, "artifacts/stage-190-window-creation-binding");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
run("dotnet", ["build", "-c", "Release", join(stage, "Fixture/WindowCreationBinding.csproj")]);
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/WindowCreationBinding.dll"));

async function verify({ compiler, image }) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }

    const input = upload(decode(image));
    const encoder = new TextEncoder();
    const names = ["Create", "CreateDefaults"];
    const entries = [];
    for (const name of names) {
        const id = encoder.encode(name);
        const row = tool.FindRow(input, upload(id));
        if (row < 1) throw Error("Missing fixture entry " + name);
        const entry = new Uint8Array(8 + id.length);
        const view = new DataView(entry.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, id.length, true);
        entry.set(id, 8);
        entries.push(...entry);
    }
    const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const size = tool.CompileModuleDetailed(input, upload(entries), output, diagnostic);
    if (size < 1)
        throw Error("Compiler status " + size + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const calls = [];
    const guest = new WebAssembly.Instance(module, {
        "standardwasm:resources": { memory: new WebAssembly.Memory({ initial: 1 }) },
        "standardwasm:win32": { createWindowExW(...args) {
            calls.push(args);
            return calls.length === 1 ? 0x76543210 : 0;
        } }
    }).exports;
    const args = [0x80000001 | 0, 65530, 123456, 0xf1000004 | 0, -5, 6, 601, 402, -3, 77, 88, 0x88550099 | 0];
    return {
        args, calls, handle: guest.Create(...args), failedHandle: guest.CreateDefaults(),
        imports: WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind])
    };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64") };
const node = await verify(input);
assert.deepEqual(node.calls, [node.args, Array(12).fill(0)]);
assert.equal(node.handle, 0x76543210);
assert.equal(node.failedHandle, 0);
assert.deepEqual(node.imports, [
    ["standardwasm:resources", "memory", "memory"],
    ["standardwasm:win32", "createWindowExW", "function"]
]);
const browser = await chromium.launch({ channel: process.env.STANDARDWASM_BROWSER_CHANNEL || "msedge" });
try {
    const page = await browser.newPage();
    const edge = await page.evaluate(verify, input);
    assert.deepEqual(edge, node);
    mkdirSync(evidence, { recursive: true });
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"),
        nodeVersion: process.version, browserVersion: browser.version(), node, edge
    }, null, 2));
    console.log("Node and Edge passed raw CreateWindowExW forwarding, default wrappers, and HWND return.");
} finally {
    await browser.close();
}
