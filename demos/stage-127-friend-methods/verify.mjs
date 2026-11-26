import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";

// Stage 127 (decision 0055): bind internal and protected internal methods across friend
// assemblies. The CLR loads each caller with the provider under test and is the oracle.
const stage = join(root, "demos", "stage-127-friend-methods");
const compiler = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts", "StandardWasm.Compiler.wasm");
if (!existsSync(compiler)) throw Error("Build the self-hosted compiler with npm test first.");
const output = join(root, "artifacts", "friend-methods");
mkdirSync(output, { recursive: true });
const evidence = mkdtempSync(join(output, "run-"));

for (const project of ["Provider/Ungranted", "Caller/Unsigned", "Caller/Signed", "Caller/WrongKey"])
    run("dotnet", ["build", "-c", "Release", join(stage, project)]);
run("dotnet", ["run", "-c", "Release", "--project", join(stage, "Oracle"), "--", evidence, stage]);
const oracle = JSON.parse(readFileSync(join(evidence, "oracle.json"), "utf8"));
const files = [compiler, ...oracle.files].map(path => readFileSync(path));

async function runCases(compiler, images, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    const capacity = 1 << 20;
    const output = build.CreateBytes(capacity);
    const location = build.CreateDiagnostic();

    function upload(bytes) {
        const array = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(array, i, bytes[i]);
        return array;
    }

    function bundle(ids) {
        const parts = ids.map(id => new Uint8Array(images[id]));
        const bytes = new Uint8Array(parts.reduce((size, part) => size + 4 + part.length, 0));
        const lengths = new DataView(bytes.buffer);
        let offset = 0;
        for (const part of parts) {
            lengths.setUint32(offset, part.length, true);
            bytes.set(part, offset + 4);
            offset += part.length + 4;
        }
        return upload(bytes);
    }

    const results = [];
    for (const test of cases) {
        const label = test.label + " " + test.name;
        const entries = new Uint8Array(11);
        new DataView(entries.buffer).setUint32(0, test.row, true);
        entries.set([3, 0, 0, 0, 114, 117, 110], 4);
        if (test.status !== undefined)
            for (let i = 0; i < 4096; i++) build.SetByte(output, i, 0xa5);
        const status = build.CompileBundleDetailed(bundle(test.images), upload(entries), output, location);
        const diagnostic = [0, 1, 2].map(index => build.ReadDiagnostic(location, index));

        if (test.status !== undefined) {
            // The CLR refused this friend call; the compiler must refuse it at the same call.
            if (status !== test.status || diagnostic.join(",") !== test.diagnostic.join(","))
                throw Error(label + " returned " + status + " at " + diagnostic + ", expected " + test.status + " at " + test.diagnostic);
            for (let i = 0; i < 4096; i++)
                if (build.ReadByte(output, i) !== 0xa5) throw Error(label + " changed the output on failure");
            results.push({ label, status, diagnostic });
            continue;
        }

        if (status < 1 || status > capacity) throw Error(label + " returned " + status + " at " + diagnostic);
        const code = Uint8Array.from({ length: status }, (_, i) => build.ReadByte(output, i));
        const { instance, module } = await WebAssembly.instantiate(code);
        if (WebAssembly.Module.imports(module).length) throw Error(label + " has runtime imports");
        const values = test.inputs.map(input => String(instance.exports.run(input)));
        if (values.join(",") !== test.clr.join(",")) throw Error(label + " returned " + values + ", the CLR returned " + test.clr);
        results.push({ label, values });
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
    writeFileSync(join(evidence, "results.json"), JSON.stringify({ node: process.version, edge: browser.version(), results: node }, null, 2) + "\n");
    const accepted = node.filter(result => result.values);
    const comparisons = accepted.reduce((count, result) => count + result.values.length, 0);
    console.log("Node " + process.version + " and Edge " + browser.version() + " matched " + comparisons +
        " CLR call results and " + (node.length - accepted.length) + " CLR access denials.");
} finally { await browser.close(); }
