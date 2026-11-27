import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "../../scripts/toolchain.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { openExecutable } from "../stage-73-executable-intake/loader/intake.mjs";
import { launchFileSystem, runManagedEntry } from "../stage-74-executable-entry/launch.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { createFilePathHost } from "../stage-74-executable-entry/long-path-host.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { win32Upcase } from "../stage-74-executable-entry/win32-upcase.mjs";

const stage = join(root, "demos/stage-360-launch-long-paths");
const evidence = join(root, "artifacts/stage-360-launch-long-paths");
const publish = join(evidence, "publish");
const exe = "LaunchLongPaths.exe", dll = "LaunchLongPaths.dll";
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
rmSync(publish, { recursive: true, force: true });
mkdirSync(evidence, { recursive: true });
run("dotnet", ["publish", join(stage, "App/LaunchLongPaths.csproj"), "-c", "Release", "--no-self-contained", "-o", publish]);

// Windows x86: the original publish, started from its own folder.
const runtime = wasm32Runtime();
const native = spawnSync(runtime.host, ["exec", "--fx-version", runtime.version, join(publish, dll)], {
    cwd: publish, windowsHide: true, encoding: "utf8"
});
if (native.error) throw native.error;
assert.ok(native.status > 0, "Windows fixture status " + native.status + " " + native.stderr);
const oraclePath = join(evidence, "oracle.json");
const matrixDirectory = join(evidence, "matrix");
rmSync(matrixDirectory, { recursive: true, force: true });
runOn32BitClr(runtime, join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"),
    [oraclePath, publish, matrixDirectory]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
// -1 to -4 are the fixture's own failure codes (disturbed saved error, bad prefix, ...).
const sentinel = value => BigInt(value) >= -4n && BigInt(value) < 0n;
assert.ok(!Object.values(oracle.results).some(sentinel), "Windows rows must be measurements");

// The host's case-insensitive table must be Windows' table for every UTF-16 unit.
const upcaseDifferences = oracle.upcase.flatMap((unit, index) => unit === win32Upcase(index) ? [] : [index]);
assert.deepEqual(upcaseDifferences, [], "win32-upcase.mjs differs from RtlUpcaseUnicodeChar");

function listFiles(directory) {
    return readdirSync(directory, { withFileTypes: true, recursive: true }).filter(entry => entry.isFile())
        .map(entry => relative(publish, join(entry.parentPath, entry.name)).replaceAll("\\", "/"));
}
const files = listFiles(publish).map(path => ({ path, bytes: readFileSync(join(publish, path)) }));
const selection = openExecutable(files, exe);
const defaultMount = launchFileSystem(selection);

// The same mount at the real Windows folder, with the DOS aliases Windows gave its ancestors.
const longNames = oracle.launch.slice(3).split("\\"), shortNames = oracle.shortDirectory.slice(3).split("\\");
assert.equal(shortNames.length, longNames.length);
const mirrorMount = {
    currentDirectory: oracle.launch,
    longPathMount: { ...defaultMount.longPathMount, root: oracle.launch,
        rootDosNames: shortNames.map((name, index) => name.toUpperCase() === longNames[index].toUpperCase() ? null : name) }
};
const aliasInput = oracle.shortDirectory + "\\Data \u03a9\\Wide \u03a9\u{1f642}.txt";

// The naming matrix's own folder, holding exactly the entries Windows created.
const matrixMount = {
    currentDirectory: oracle.matrix.root,
    longPathMount: { root: oracle.matrix.root, entries: [
        ...oracle.matrix.directories.map(path => ({ path, kind: "directory" })),
        ...oracle.matrix.files.map(path => ({ path, kind: "file" }))
    ] }
};
const matrixQueries = oracle.matrix.rows.map(({ path, capacity }) => ({ path, capacity }));

async function verify({ compiler, image, scenarios, defaultMount, mirrorMount, aliasInput, matrixMount, matrixQueries },
    instantiate, createFilePathHost) {
    const decode = base64 => Uint8Array.from(atob(base64), value => value.charCodeAt(0));
    const { instance: { exports: tool } } = await WebAssembly.instantiate(decode(compiler));
    function upload(bytes) {
        const result = tool.CreateBytes(bytes.length);
        bytes.forEach((byte, index) => tool.SetByte(result, index, byte));
        return result;
    }
    const input = upload(decode(image));
    function compile(name) {
        const row = tool.FindRow(input, upload(new TextEncoder().encode(name)));
        if (row < 1) throw Error("Missing fixture entry " + name);
        const output = tool.CreateBytes(1 << 18), diagnostic = tool.CreateDiagnostic();
        const length = tool.CompileDetailed(input, row, output, diagnostic);
        if (length < 1) throw Error(name + ": compiler " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
        const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
        if (!WebAssembly.Module.imports(module).some(entry => entry.module === "standardwasm:win32" && entry.name === "getLongPathNameW"))
            throw Error("Expected the production long-path Win32 import.");
        return module;
    }
    const { exports: observe } = await instantiate(compile("Observe"), tool, [], {}, "LaunchLongPaths.exe",
        undefined, undefined, { win32FileSystem: defaultMount });
    const results = Object.fromEntries(scenarios.map(index => [index, observe.run(Number(index)).toString()]));

    // Oracle case 14 needs the real folder's DOS-aliased ancestors, and the naming
    // matrix needs its own folder, which only the host configuration can name, so
    // these rows call the production provider directly with a '#'-filled buffer.
    function lookUp(win32FileSystem, path, capacity) {
        const memory = new WebAssembly.Memory({ initial: 1 }), errors = { system: 0, pinvoke: 0 };
        // Address 0 is the null pointer, so the input starts at 16.
        const view = new DataView(memory.buffer), source = 16, target = 8192;
        for (let index = 0; index < path.length; index++) view.setUint16(source + 2 * index, path.charCodeAt(index), true);
        view.setUint16(source + 2 * path.length, 0, true);
        for (let index = 0; index < 1026; index++) view.setUint16(target + 2 * index, 35, true);
        const length = createFilePathHost(memory, win32FileSystem, errors).getLongPathNameW(source, target, capacity);
        const units = new Uint16Array(memory.buffer, target, 1026);
        let changed = 0;
        while (changed < 1026 && units[changed] !== 35) changed++;
        if (units.subarray(changed).some(unit => unit !== 35)) throw Error("Non-contiguous write for " + path);
        return { length, error: length === 0 ? errors.system : 0, written: String.fromCharCode(...units.subarray(0, changed)) };
    }
    const alias = lookUp(mirrorMount, aliasInput, 1024);
    // The production volume has no 8.3 names: an alias-shaped ancestor names nothing.
    const unaliased = lookUp(defaultMount, "C:\\STANDA~1\\App\\LaunchLongPaths.dll", 1024);
    const matrix = matrixQueries.map(({ path, capacity }) => ({ path, capacity, result: lookUp(matrixMount, path, capacity) }));
    return { results, alias, unaliased, matrix };
}

const image = selection.entry.bytes;
const input = {
    compiler: compiler.toString("base64"), image: Buffer.from(image).toString("base64"),
    scenarios: Object.keys(oracle.results), defaultMount, mirrorMount, aliasInput, matrixMount, matrixQueries
};
const node = await verify(input, instantiateManagedModule, createFilePathHost);
assert.deepEqual(node.results, oracle.results);
assert.equal(oracle.aliasInput, aliasInput);
assert.ok(oracle.alias.length > 0, "Windows expands the aliased ancestors");
assert.deepEqual(node.alias, oracle.alias);
assert.deepEqual(node.unaliased, { length: 0, error: 3, written: "" });
assert.deepEqual(node.matrix, oracle.matrix.rows);

// The unchanged launch path: the exit status folds every scenario.
const nodeLaunch = await runManagedEntry(compiler, selection);
assert.equal(nodeLaunch.state, "finished", JSON.stringify(nodeLaunch));
assert.equal(nodeLaunch.exitCode, native.status);

const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.goto(server.url + "/?run");
    const edge = await page.evaluate(async ({ input, source }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createFilePathHost } = await import("/launch/long-path-host.mjs");
        return (0, eval)("(" + source + ")")(input, instantiateManagedModule, createFilePathHost);
    }, { input, source: verify.toString() });
    assert.deepEqual(edge, node);

    // Drop the original publish folder into the Edge launcher and its worker.
    await page.locator("#folder").setInputFiles(publish);
    await page.waitForFunction(() => document.querySelector("#status").dataset.state === "ready");
    await page.getByRole("button", { name: "Run managed entry" }).click();
    await page.waitForFunction(() => ["finished", "unsupported", "framework-missing", "error"].includes(
        document.querySelector("#status").dataset.state));
    const status = page.locator("#status");
    const report = await status.textContent();
    assert.equal(await status.getAttribute("data-state"), "finished", report);
    const edgeLaunch = JSON.parse(report);
    assert.deepEqual(edgeLaunch, nodeLaunch);

    const sha = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha(compiler), clr: oracle.clr, nodeVersion: process.version, edgeVersion: browser.version(),
        native: native.status, nodeLaunch, edgeLaunch, oracle: oracle.results, node, edge,
        source: files.map(({ path, bytes }) => ({ path, sha256: sha(bytes) }))
    }, null, 2) + "\n");
    console.log("Windows x86, Node and the Edge launch worker agreed on " + input.scenarios.length +
        " GetLongPathNameW rows in the launch directory, " + node.matrix.length +
        " naming-matrix rows, the aliased-ancestor row and exit status " + native.status + ".");
} finally {
    await browser?.close();
    await server.close();
}
