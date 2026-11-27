import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stage = join(root, "demos", "stage-306-native-byte-transfer");
const evidence = join(root, "artifacts", "stage-306-native-byte-transfer");
mkdirSync(evidence, { recursive: true });
const runtime = wasm32Runtime();
const mono = process.env.STANDARDWASM_MONO_CORELIB || join(process.env.USERPROFILE, ".nuget", "packages",
    "microsoft.netcore.app.runtime.mono.browser-wasm", runtime.version, "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
const oraclePath = join(evidence, "oracle.json");
const helperPath = join(evidence, "transfer-emitter.wasm");
runOn32BitClr(runtime, join(stage, "Oracle", "Oracle.csproj"),
    join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"), [oraclePath, helperPath, mono]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compiler = readFileSync(process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm"));
const fixture = readFileSync(oracle.assembly);
const helper = readFileSync(helperPath);
const images = [fixture, ...["netstandard.dll", "System.Runtime.dll", "System.Runtime.InteropServices.dll"].map(name =>
    readFileSync(join(runtime.directory, name))), readFileSync(mono)];
const bundle = Buffer.concat(images.flatMap(image => {
    const size = Buffer.alloc(4);
    size.writeUInt32LE(image.length);
    return [size, image];
}));

async function verify(compiler, bundle, fixture, helper, { rows, cases, input }) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const array = tool.CreateBytes(bytes.length);
        for (let index = 0; index < bytes.length; index++) tool.SetByte(array, index, bytes[index]);
        return array;
    }
    function entries(exports) {
        return Uint8Array.from(Object.entries(exports).flatMap(([name, row]) => [
            row & 255, (row >> 8) & 255, (row >> 16) & 255, 0, name.length, 0, 0, 0,
            ...Array.from(name, character => character.charCodeAt(0))
        ]));
    }
    function copiedOutput(length) {
        return Uint8Array.from({ length }, (_, index) => tool.ReadByte(output, index));
    }
    const output = tool.CreateBytes(2 << 20);
    const diagnostic = tool.CreateDiagnostic();
    const transferStatus = tool.CompileBundleDetailed(upload(new Uint8Array(bundle)),
        upload(entries(rows)), output, diagnostic);
    const wrapper = transferStatus < 0 ?
        { status: transferStatus, location: [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)) } : null;
    const wrapperBytes = transferStatus > 0 ? copiedOutput(transferStatus) : null;
    const fixtureLength = tool.CompileModuleDetailed(upload(new Uint8Array(fixture)),
        upload(entries({ make: rows.make, read: rows.read })), output, diagnostic);
    if (fixtureLength <= 0) throw Error("Byte-vector fixture compile " + fixtureLength);
    const { instance: { exports: data } } = await WebAssembly.instantiate(copiedOutput(fixtureLength));

    async function execute(bytes, vectors, samples) {
        const module = await WebAssembly.compile(bytes);
        const declarations = WebAssembly.Module.imports(module);
        if (declarations.length !== 1 || declarations[0].module !== "standardwasm:resources" ||
            declarations[0].name !== "memory" || declarations[0].kind !== "memory") throw Error("Unexpected transfer import");
        const memory = new WebAssembly.Memory({ initial: 1 });
        const memoryBytes = new Uint8Array(memory.buffer);
        const sourceStart = memoryBytes.length - input.length;
        memoryBytes.set(input, sourceStart);
        const exports = await WebAssembly.instantiate(module, { "standardwasm:resources": { memory } }).then(result => result.exports);
        const outcomes = samples.map(({ Name, Source, Destination, Start, Count }) => {
            const array = Destination < 0 ? null : vectors.make(Destination);
            const alias = array;
            const source = Source === 1 ? 0 : Source === 2 ? -1 : sourceStart;
            let code = "ok";
            try {
                exports.transfer(source, array, Start, Count);
            } catch (error) {
                if (!(error instanceof WebAssembly.Exception)) throw error;
                if (exports["\0argumentNull"] && error.is(exports["\0argumentNull"])) code = "ArgumentNullException";
                else if (error.is(exports["\0argumentOutOfRange"])) code = "ArgumentOutOfRangeException";
                else throw error;
            }
            const values = alias === null ? [] : Array.from({ length: Destination }, (_, index) => vectors.read(alias, index));
            return ["TRANSFER", Name, code, ...values].join("|");
        });
        const atomic = vectors.make(3);
        try {
            exports.transfer(memoryBytes.length - 1, atomic, 0, 3);
            throw Error("Expected bounded native transfer to trap");
        } catch (error) {
            if (!(error instanceof WebAssembly.RuntimeError)) throw error;
        }
        if (Array.from({ length: 3 }, (_, index) => vectors.read(atomic, index)).some(value => value !== 90))
            throw Error("Invalid native extent partially wrote the destination");
        return outcomes;
    }

    // Invoke the exact production emitter independently of the part of the
    // genuine guard's formatted exception that is still blocked.
    const outcomes = await execute(helper, data, cases.filter(test => test.Source !== 1 && test.Destination >= 0));
    const linked = wrapperBytes ? await execute(wrapperBytes, data, cases) : null;
    return { wrapper, outcomes, linked, atomicInvalidExtent: true };
}

const node = await verify(compiler, bundle, fixture, helper, oracle);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    const assets = { "/compiler": compiler, "/bundle": bundle, "/fixture": fixture, "/helper": helper };
    await page.route("http://127.0.0.1/**", route => {
        const body = assets[new URL(route.request().url()).pathname];
        return body ? route.fulfill({ body, contentType: "application/octet-stream" }) :
            route.fulfill({ body: "<!doctype html>", contentType: "text/html" });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ source, oracle }) => {
        const images = await Promise.all(["/compiler", "/bundle", "/fixture", "/helper"].map(async path => (await fetch(path)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(...images, oracle);
    }, { source: verify.toString(), oracle });
    assert.deepEqual(edge, node);
    const expected = oracle.report.trimEnd().split("\n");
    assert.deepEqual(node.outcomes, expected.filter(line => !line.includes("ArgumentNullException")));
    if (node.wrapper) assert.deepEqual(node.wrapper, { status: -2, location: [4, oracle.wrapperFrontier, 31] });
    else assert.deepEqual(node.linked, expected);
    const hash = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: hash(compiler), assembly: hash(fixture), node: process.version, edge: browser.version(),
        clr: oracle.runtime, ...node
    }, null, 2) + "\n");
    console.log("Stage 306 native byte transfer backend passed", node.outcomes.length, "CLR/Node/Edge cases.");
    if (node.wrapper) console.log("The original Marshal.Copy guard still reaches String.JoinCore.");
} finally {
    await browser.close();
}
