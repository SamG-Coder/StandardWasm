import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-307-com-cookie-lookup"), evidence = join(root, "artifacts/stage-307-com-cookie-lookup");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const { clr, assembly, dependencies, ...oracle } = JSON.parse(readFileSync(oraclePath, "utf8"));
const pack = Buffer.concat([assembly, ...dependencies].flatMap(path => {
    const bytes = readFileSync(path), header = Buffer.alloc(4);
    header.writeUInt32LE(bytes.length);
    return [header, bytes];
}));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, pack, oracle, instantiate) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    function compile(entries) {
        const manifest = [];
        for (const [row, name] of entries) {
            const text = new TextEncoder().encode(name), entry = new Uint8Array(text.length + 8);
            const header = new DataView(entry.buffer);
            header.setUint32(0, row, true);
            header.setUint32(4, text.length, true);
            entry.set(text, 8);
            manifest.push(...entry);
        }
        return tool.CompileBundleDetailed(bundle, upload(manifest), output, diagnostic);
    }

    const bundle = upload(new Uint8Array(pack)), output = tool.CreateBytes(2 << 20), diagnostic = tool.CreateDiagnostic();
    const size = compile([[oracle.row, "lookup"], [oracle.wrappedRow, "wrapped"]]);
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    if (size < 1) throw Error(`COM fixture compilation ${size} at ${location()}`);
    const module = await WebAssembly.compile(Uint8Array.from({ length: size }, (_, i) => tool.ReadByte(output, i)));
    const imports = WebAssembly.Module.imports(module);
    const signature = "stdcall:i32(p32,u32,p32,p32)", identity = 0x1234;
    let liveCookie = true, table, wrapper, wrong, iid, result, memory;
    const words = () => new DataView(memory.buffer);
    function checkArguments(expected, receiver, id, output) {
        if (receiver !== expected || id !== iid || output !== result ||
            oracle.iid.some((value, index) => words().getUint8(id + index) !== value))
            throw Error("COM dispatch changed the receiver or pointer arguments");
    }
    const guest = await instantiate(module, tool, [], {}, null, null, registry => {
        ({ memory } = registry);
        table = registry.registerInterface("IGlobalInterfaceTable", new Map([[5, { signature,
            invoke(receiver, cookie, id, output) {
                checkArguments(table, receiver, id, output);
                if (cookie !== 42 && cookie !== 0xffffffff) throw Error("The cookie lost its unsigned value");
                const hit = cookie === 42 && liveCookie;
                words().setUint32(output, hit ? identity : 0, true);
                return hit ? 0 : -2147024809;
            }
        }]]));
        wrapper = registry.registerInterface("WrappedLookup", new Map([[5, { signature,
            invoke(receiver, cookie, id, output) {
                checkArguments(wrapper, receiver, id, output);
                if (cookie !== 0xffffffff) throw Error("The wrapped lookup lost its cookie");
                words().setUint32(output, 37, true);
                return -2147467259;
            }
        }]]));
        wrong = registry.registerInterface("DifferentAbi", new Map([[5, {
            signature: "stdcall:i32(p32)", invoke() { throw Error("Called the wrong signature"); }
        }]]));
    });
    iid = memory.grow(1) * 65536;
    result = iid + 16;
    new Uint8Array(memory.buffer, iid, 16).set(oracle.iid);
    function lookup(cookie) {
        words().setUint32(result, 17, true);
        const hr = guest.exports.lookup(table, cookie, iid, result), output = words().getUint32(result, true);
        return { hr, original: output === identity, cleared: output === 0 };
    }
    const live = lookup(42), missing = lookup(0xffffffff);
    liveCookie = false;
    const revoked = lookup(42);
    const wrapped = { hr: guest.exports.wrapped(wrapper, 0xffffffff, iid, result), marker: words().getUint32(result, true) };
    let wrongSignature = false;
    try { guest.exports.lookup(wrong, 42, iid, result); }
    catch (error) { wrongSignature = /unregistered COM receiver or call token/.test(error.message); }
    const status = compile([[oracle.rejected, "wrong"]]);
    return { live, missing, revoked, wrapped, wrongSignature, imports, rejection: { status, location: location() } };
}

const node = await exercise(compiler, pack, oracle, instantiateManagedModule);
for (const name of ["live", "missing", "revoked", "wrapped"]) assert.deepEqual(node[name], oracle[name]);
assert.equal(node.wrongSignature, true);
assert.equal(node.rejection.status, -2);
assert.deepEqual(node.rejection.location, [0, oracle.rejected, 27]);
assert.deepEqual(node.imports, [
    { module: "standardwasm:resources", name: "memory", kind: "memory" },
    { module: "standardwasm:com", name: "stdcall:i32(p32,u32,p32,p32)", kind: "function" }
]);
const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/com-fixture.pack", route => route.fulfill({ body: pack, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ code, oracle }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/com-fixture.pack"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(`${path} returned HTTP ${response.status}`);
            return response.arrayBuffer();
        }));
        return (0, eval)(`(${code})`)(...assets, oracle, instantiateManagedModule);
    }, { code: exercise.toString(), oracle });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const result = { compiler: sha256(compiler), assembly: sha256(readFileSync(assembly)), clr,
        node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
    console.log(JSON.stringify(result));
} finally {
    await browser?.close();
    await server.close();
}
