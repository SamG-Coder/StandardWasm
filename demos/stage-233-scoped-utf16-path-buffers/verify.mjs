import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";

const stage = join(root, "demos/stage-233-scoped-utf16-path-buffers");
const evidence = join(root, "artifacts/stage-233-scoped-utf16-path-buffers");
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm"));
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(join(stage, "Fixture/bin/Release/net10.0/ScopedPathBuffers.dll"));

async function verify({ compiler, image, names }) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    const win32 = "standardwasm:win32";
    const resources = "standardwasm:resources";
    const knownPath = "Long Folder \u03bb\\Long File \u03a9\u{1f642}.txt";
    const missingLeaf = "Long Folder \u03bb\\not-present.txt";
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const input = upload(decode(image));
    const output = tool.CreateBytes(1 << 18);
    const diagnostic = tool.CreateDiagnostic();
    const results = {}, calls = {};
    for (const name of names) {
        const row = tool.FindRow(input, upload(new TextEncoder().encode(name)));
        if (row < 1) throw Error("Missing fixture export " + name);
        const length = tool.CompileDetailed(input, row, output, diagnostic);
        if (length < 1)
            throw Error(name + " status " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
        const imports = WebAssembly.Module.imports(module);
        if (imports.length !== 6 ||
            !imports.some(entry => entry.module === win32 && entry.name === "getLongPathNameW" && entry.kind === "function") ||
            !imports.some(entry => entry.module === resources && entry.name === "memory" && entry.kind === "memory"))
            throw Error("Unexpected Win32 imports for " + name + ": " + JSON.stringify(imports));
        const memory = new WebAssembly.Memory({ initial: 1 });
        let system = 0, pinvoke = 0;
        // A stage-local oracle provider accepts only the paths represented by the
        // Windows fixture. It keeps filesystem policy out of compiler verification.
        function getLongPathNameW(source, destination, capacity) {
            const view = new DataView(memory.buffer);
            let path = "";
            for (let offset = source; ; offset += 2) {
                const unit = view.getUint16(offset, true);
                if (unit === 0) break;
                path += String.fromCharCode(unit);
                if (path.length > 128) throw Error("Missing terminator");
            }
            if (calls[name]) throw Error("Expected a single native call for " + name);
            calls[name] = { capacity, sameAddress: source === destination, system, pinvoke };
            if (path === missingLeaf) { system = 2; return 0; }
            if (path === "not-present\\not-present.txt") { system = 3; return 0; }
            if (path !== knownPath) throw Error("Unexpected fixture path " + path);
            if (capacity <= path.length) return path.length + 1;
            for (let i = 0; i < path.length; i++) view.setUint16(destination + i * 2, path.charCodeAt(i), true);
            view.setUint16(destination + path.length * 2, 0, true);
            return path.length;
        }
        const { exports } = await WebAssembly.instantiate(module, {
            [resources]: { memory },
            [win32]: {
                getLongPathNameW,
                getLastSystemError: () => system,
                setLastSystemError: value => { system = value; },
                getLastPInvokeError: () => pinvoke,
                setLastPInvokeError: value => { pinvoke = value; }
            }
        });
        results[name] = exports.run();
    }
    return { results, calls };
}

const input = { compiler: compiler.toString("base64"), image: image.toString("base64"), names: Object.keys(oracle.results) };
const node = await verify(input);
assert.deepEqual(node.results, oracle.results);
assert.ok(Object.values(node.results).every(value => value === 1), JSON.stringify(node));
assert.equal(node.calls.NativeInPlace.sameAddress, true);
assert.equal(node.calls.Query.capacity, 0);
assert.equal(node.calls.RawQuery.system, 17185);
assert.equal(node.calls.RawQuery.pinvoke, 25090);
for (const [name, call] of Object.entries(node.calls))
    if (name !== "RawQuery") assert.equal(call.system, 0);

const server = await serveLaunch(0, new Uint8Array());
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url);
    const edge = await page.evaluate(({ input, source }) => (0, eval)("(" + source + ")")(input), { input, source: verify.toString() });
    assert.deepEqual(edge, node);
    const sha256 = data => createHash("sha256").update(data).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(image),
        clr: oracle.clr, oracle: oracle.results, nodeVersion: process.version, edgeVersion: browser.version(), node, edge
    }, null, 2) + "\n");
    console.log("Node and Edge matched Windows x86 CLR on " + input.names.length + " scoped UTF-16 path cases.");
} finally {
    await browser?.close();
    await server.close();
}
