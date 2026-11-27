import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";

// Stage 129 (decision 0057): run reachable type initializers with CLI trigger semantics,
// and allocate plain System.Object instances. The bundle carries the supported 32-bit
// CoreLib pack, and the x86 CLR running the same DLLs is the oracle.
const stage = join(root, "demos", "stage-129-type-initializers");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "type-initializers");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

run("dotnet", ["build", "-c", "Release", join(stage, "Fixture")]);
const bin = join(stage, "Fixture", "bin", "Release", "netstandard2.0");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle", "bin", "Release", "net10.0", "Oracle.dll"),
    [evidence, join(bin, "TypeInitializerFixture.dll"), join(bin, "TypeInitializerDependency.dll")]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
assert.equal(oracle.process, "X86");
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 21;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();
    const bundles = new Map();

    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }

    function bundle(ids) {
        const key = ids.join(",");
        if (bundles.has(key)) return bundles.get(key);
        const parts = ids.map(id => new Uint8Array(images[id]));
        const bytes = new Uint8Array(parts.reduce((size, part) => size + 4 + part.length, 0));
        const lengths = new DataView(bytes.buffer);
        let offset = 0;
        for (const part of parts) {
            lengths.setUint32(offset, part.length, true);
            bytes.set(part, offset + 4);
            offset += part.length + 4;
        }
        const linked = upload(bytes);
        bundles.set(key, linked);
        return linked;
    }

    // A trap from the compiler's failed-initializer path is an unreachable RuntimeError.
    // A raw engine trap (stack exhaustion) is any other non-managed error.
    // Issue #170: a failed initializer raises CoreLib's TypeInitializationException on the object
    // tag; `thrown` keeps each payload so repeated triggers can be checked for the same object.
    let thrown = [];
    function outcome(call, tag) {
        try { return String(call()); }
        catch (error) {
            if (error instanceof WebAssembly.Exception) {
                if (tag && error.is(tag)) thrown.push(error.getArg(tag, 0));
                return "managed exception";
            }
            if (error instanceof WebAssembly.RuntimeError && /unreachable/i.test(error.message)) return "trap";
            return "raw trap";
        }
    }

    const results = [];
    for (const test of cases) {
        // One manifest record per export: [MethodDef row][name length][name].
        const exports = test.exports ? test.exports.map(entry => [entry.row, entry.name]) : [[test.row, "run"]];
        const entries = new Uint8Array(exports.reduce((size, [, name]) => size + 8 + name.length, 0));
        let cursor = 0;
        for (const [row, name] of exports) {
            const view = new DataView(entries.buffer);
            view.setUint32(cursor, row, true);
            view.setUint32(cursor + 4, name.length, true);
            for (let i = 0; i < name.length; i++) entries[cursor + 8 + i] = name.charCodeAt(i);
            cursor += 8 + name.length;
        }
        if (test.status !== undefined)
            for (let i = 0; i < 4096; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle(test.images), upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(index => build.ReadDiagnostic(location, index));

        if (test.status !== undefined) {
            if (status !== test.status || diagnostic.join(",") !== test.diagnostic.join(","))
                throw Error(test.name + " returned " + status + " at " + diagnostic + ", expected " +
                    test.status + " at " + test.diagnostic);
            for (let i = 0; i < 4096; i++)
                if (build.ReadByte(output, i) !== 0xa5) throw Error(test.name + " changed the output on failure");
            results.push({ name: test.name, status, diagnostic });
            continue;
        }

        if (status < 1 || status > capacity) throw Error(test.name + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const { instance, module } = await WebAssembly.instantiate(code);
        if (WebAssembly.Module.imports(module).length) throw Error(test.name + " has runtime imports");

        if (test.sequence) {
            // Same instance throughout: a failed initializer raises the same TypeInitializationException
            // object on every trigger, runs once, and never exposes partially initialized state.
            thrown = [];
            const tag = instance.exports["\0object"];
            const observed = test.sequence.map(([name]) => [name, outcome(() => instance.exports[name](), tag)]);
            const expected = test.sequence.map(([name, value]) =>
                [name, value === "TypeInitializationException" ? "managed exception" : value]);
            if (JSON.stringify(observed) !== JSON.stringify(expected))
                throw Error(test.name + " observed " + JSON.stringify(observed) + ", expected " + JSON.stringify(expected));
            // Every expected managed exception must have arrived on the object tag (a payload was
            // observed), and the repeated triggers of ReadFailing must carry one and the same object.
            const managed = expected.filter(([, value]) => value === "managed exception").length;
            if (thrown.length !== managed)
                throw Error(test.name + " observed " + thrown.length + " object-tag payloads, expected " + managed);
            if (test.name === "ReadFailing" && (managed < 2 || thrown.some(payload => payload !== thrown[0])))
                throw Error(test.name + " did not raise one exception object on its repeated triggers");
            results.push({ name: test.name, sequence: observed });
            continue;
        }

        if (test.initializerMessage) {
            // The wrapper's literals are the CLR's own TypeName and Message (UTF-16 in a data segment).
            // Self-contained: this function also runs inside the browser page.
            const carries = text => {
                const pattern = new Uint8Array(2 * text.length);
                for (let i = 0; i < text.length; i++) {
                    pattern[2 * i] = text.charCodeAt(i) & 255;
                    pattern[2 * i + 1] = text.charCodeAt(i) >> 8;
                }
                search: for (let at = 0; at + pattern.length <= code.length; at++) {
                    for (let j = 0; j < pattern.length; j++) if (code[at + j] !== pattern[j]) continue search;
                    return true;
                }
                return false;
            };
            for (const text of [test.initializerTypeName, test.initializerMessage])
                if (!carries(text)) throw Error(test.name + " does not carry the CLR initializer string " + JSON.stringify(text));
        }
        const value = String(instance.exports.run());
        if (value !== test.wasm) throw Error(test.name + " returned " + value + ", the CLR returned " + test.clr);
        results.push({ name: test.name, value });
    }
    return results;
}

const node = await runCases(files[0], files.slice(1), oracle.cases);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("http://127.0.0.1/**", route => {
        const path = new URL(route.request().url()).pathname;
        if (path === "/") return route.fulfill({ contentType: "text/html", body: "<!doctype html>" });
        return route.fulfill({ contentType: "application/octet-stream", body: files[Number(path.slice(1))] });
    });
    await page.goto("http://127.0.0.1/");
    const edge = await page.evaluate(async ({ count, cases, source }) => {
        const [compiler, ...images] = await Promise.all(Array.from({ length: count }, async (_, i) =>
            (await fetch("/" + i)).arrayBuffer()));
        return (0, eval)("(" + source + ")")(compiler, images, cases);
    }, { count: files.length, cases: oracle.cases, source: runCases.toString() });
    assert.deepEqual(edge, node);
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        runtime: oracle.runtime, clrProcess: oracle.process, node: process.version, edge: browser.version(), results: node
    }, null, 2) + "\n");
    const values = node.filter(result => result.value !== undefined).length;
    const failures = node.filter(result => result.sequence !== undefined).length;
    const diagnostics = node.filter(result => result.status !== undefined).length;
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + values +
        " initializer and root-object results from the x86 CLR " + oracle.runtime + ", " + failures +
        " same-instance failure sequences and " + diagnostics + " located diagnostics.");
} finally { await browser.close(); }
