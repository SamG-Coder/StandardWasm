import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-195-full-path-native-binding");
const evidence = join(root, "artifacts/stage-195-full-path-native-binding");
run("dotnet", ["build", "-c", "Release", join(stage, "Fixture/FullPathNativeBinding.csproj")]);
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/FullPathNativeBinding.dll"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));

async function verify({ compiler, image }, instantiate) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const input = upload(decode(image));
    function compile(name) {
        const row = tool.FindRow(input, upload(new TextEncoder().encode(name)));
        if (row < 1) throw Error("Missing " + name + " export");
        const output = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
        const length = tool.CompileDetailed(input, row, output, diagnostic);
        if (length < 1)
            throw Error(name + " status " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        return new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
    }
    const module = compile("Query");
    const memory = new WebAssembly.Memory({ initial: 1 });
    const view = new DataView(memory.buffer);
    const calls = [];
    const instance = new WebAssembly.Instance(module, {
        "standardwasm:resources": { memory },
        "standardwasm:win32": {
            getFullPathNameW(path, capacity, buffer, filePart) {
                calls.push([path, capacity, buffer, filePart]);
                if (!buffer) return 2;
                view.setUint16(buffer, view.getUint16(path, true), true);
                view.setUint16(buffer + 2, 0, true);
                if (filePart) view.setUint32(filePart, buffer, true);
                return 1;
            }
        }
    });
    view.setUint16(64, 0x6f22, true);
    const lengthQuery = instance.exports.run(64, 0, 0, 0);
    const resolved = instance.exports.run(64, 16, 128, 256);
    const app = await instantiate(compile("ResolveRelative"), tool, [], {}, "Fixture.exe", undefined, undefined, {
        win32FileSystem: { currentDirectory: "R:\\virtual\\work" }
    });
    return {
        calls, lengthQuery, resolved, text: view.getUint16(128, true),
        terminator: view.getUint16(130, true), filePart: view.getUint32(256, true),
        relativePathResolved: app.exports.run() === 1,
        imports: WebAssembly.Module.imports(module).map(({ module, name, kind }) => [module, name, kind])
    };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64") };
const node = await verify(input, instantiateManagedModule);
assert.deepEqual(node, {
    calls: [[64, 0, 0, 0], [64, 16, 128, 256]],
    lengthQuery: 2, resolved: 1, text: 0x6f22, terminator: 0, filePart: 128,
    relativePathResolved: true,
    imports: [["standardwasm:resources", "memory", "memory"], ["standardwasm:win32", "getFullPathNameW", "function"]]
});
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
    mkdirSync(evidence, { recursive: true });
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: createHash("sha256").update(compiler).digest("hex"),
        nodeVersion: process.version, edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge verified the raw GetFullPathNameW import and resolved a UTF-16 relative path through the production host.");
} finally {
    await browser?.close();
    await server.close();
}
