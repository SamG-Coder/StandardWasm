import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";

const stageName = "stage-249-display-device-capabilities";
const stage = join(root, "demos", stageName);
const evidence = join(root, "artifacts", stageName);
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle/Oracle.csproj"),
    join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const image = readFileSync(oracle.assembly);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);
async function verify(compiler, fixture, instantiate, capabilities, useBrowserProfile) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const result = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(result, i, bytes[i]);
        return result;
    }
    const image = upload(fixture);
    const manifest = [];
    const encoder = new TextEncoder();
    for (const name of ["GetDesktop", "Capability", "ReleaseDesktop"]) {
        const text = encoder.encode(name);
        const entry = new Uint8Array(8 + text.length), words = new DataView(entry.buffer);
        const row = tool.FindRow(image, upload(text));
        if (row < 1) throw Error("Fixture export is missing: " + name);
        words.setUint32(0, row, true);
        words.setUint32(4, text.length, true);
        entry.set(text, 8);
        manifest.push(...entry);
    }
    const output = tool.CreateBytes(1 << 18), diagnostic = tool.CreateDiagnostic();
    const length = tool.CompileModuleDetailed(image, upload(manifest), output, diagnostic);
    if (length < 1) throw Error("Compiler status " + length + " at " + [0, 1, 2].map(i => tool.ReadDiagnostic(diagnostic, i)));
    const module = new WebAssembly.Module(Uint8Array.from({ length }, (_, i) => tool.ReadByte(output, i)));
    const make = profile => instantiate(module, tool, [], {}, "Fixture.exe", undefined, undefined, {
        displayCapabilities: profile
    });
    const { exports: app } = await make(capabilities);
    const { exports: other } = await make(capabilities);
    const handle = app.GetDesktop();
    if (!handle) throw Error("Browser did not return a screen device context.");
    const values = Object.fromEntries([12, 14, 88, 90].map(selector => [selector, app.Capability(handle, selector)]));
    const foreignCapability = other.Capability(handle, 88);
    const foreignRelease = other.ReleaseDesktop(handle);
    let unsupported = false;
    try { app.Capability(handle, -1); }
    catch (error) { unsupported = error instanceof Error && error.message.includes("selector -1"); }
    const release = app.ReleaseDesktop(handle);
    const shared = {
        capabilities: values, release, nullCapability: app.Capability(0, 88), nullRelease: app.ReleaseDesktop(0),
        foreignCapability, foreignRelease, staleCapability: app.Capability(handle, 88), staleRelease: app.ReleaseDesktop(handle),
        unsupported, imports: WebAssembly.Module.imports(module).map(item => item.module + "/" + item.name).sort()
    };
    if (!useBrowserProfile) return shared;
    const { exports: display } = await make(undefined);
    const context = display.GetDesktop();
    const actual = [display.Capability(context, 12), display.Capability(context, 88), display.Capability(context, 90)];
    const dpi = Math.max(1, Math.round(96 * devicePixelRatio));
    return { shared, actual, expected: [screen.colorDepth, dpi, dpi], released: display.ReleaseDesktop(context) };
}

const node = await verify(compiler, image, instantiateManagedModule, oracle.capabilities, false);
const expected = {
    capabilities: oracle.capabilities, release: oracle.release, nullCapability: oracle.nullCapability, nullRelease: oracle.nullRelease,
    foreignCapability: 0, foreignRelease: 0, staleCapability: 0, staleRelease: 0, unsupported: true,
    imports: ["getDC", "getDeviceCaps", "releaseDC"].map(name => "standardwasm:win32/" + name)
};
assert.deepEqual(node, expected);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const server = await serveLaunch(0, compilerPath);
    try {
        const page = await browser.newPage({ deviceScaleFactor: 2 });
        await page.goto(server.url);
        const edge = await page.evaluate(async ({ code, fixture, capabilities }) => {
            const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
            const response = await fetch("/StandardWasm.Compiler.wasm");
            if (!response.ok) throw Error("The self-hosted compiler is unavailable.");
            const compiler = await response.arrayBuffer();
            return (0, eval)("(" + code + ")")(compiler, new Uint8Array(fixture), instantiateManagedModule, capabilities, true);
        }, { code: verify.toString(), fixture: [...image], capabilities: oracle.capabilities });
        assert.deepEqual(edge.shared, expected);
        assert.deepEqual(edge.actual, edge.expected);
        assert.equal(edge.released, 1);
        const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
        const result = { compiler: sha256(compiler), assembly: sha256(image), clr: oracle.runtime,
            node: process.version, edge: browser.version(), results: node, edgeDisplay: edge.actual };
        writeFileSync(join(evidence, "results.json"), JSON.stringify(result, null, 2) + "\n");
        console.log(JSON.stringify(result));
    } finally {
        await server.close();
    }
} finally {
    await browser.close();
}
