import assert from "node:assert/strict";
import { chromium } from "playwright-core";
import { packEntry } from "../demos/stage-74-executable-entry/companions.mjs";

async function execute(compilerBytes, bundle, entries, exercise, scenario) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(compilerBytes);
    const encoder = new TextEncoder();
    const names = entries.map(({ name }) => encoder.encode(name));
    const manifest = new Uint8Array(names.reduce((size, bytes) => size + 8 + bytes.length, 0));
    const view = new DataView(manifest.buffer);
    let offset = 0;
    for (const [index, { row }] of entries.entries()) {
        const name = names[index];
        view.setUint32(offset, row, true);
        view.setUint32(offset + 4, name.length, true);
        manifest.set(name, offset + 8);
        offset += 8 + name.length;
    }
    function upload(bytes) {
        const input = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(input, i, bytes[i]);
        return input;
    }
    const capacity = 1 << 20;
    const buffer = compiler.CreateBytes(capacity), location = compiler.CreateDiagnostic();
    const size = compiler.CompileBundleDetailed(upload(bundle), upload(manifest), buffer, location);
    if (size < 1 || size > capacity)
        throw Error("Compiler returned " + size + " at " + [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index)));
    const code = Uint8Array.from({ length: size }, (_, index) => compiler.ReadByte(buffer, index));
    const { instance } = await WebAssembly.instantiate(code);
    return exercise(instance.exports, scenario);
}

// The scenario receives the same generated exports in Node and in Edge.
// Only serializable observations cross the browser boundary.
export async function verifyLinkedDemo(compiler, fixture, companions, entries, exercise, scenario) {
    const [bundle] = packEntry(fixture, companions, entries[0].row);
    const observed = await execute(compiler, bundle, entries, exercise, scenario);
    const assets = new Map([["/compiler", compiler], ["/bundle", bundle]]);
    const browser = await chromium.launch({ channel: "msedge" });
    try {
        const page = await browser.newPage();
        await page.route("http://localhost/**", route => {
            const bytes = assets.get(new URL(route.request().url()).pathname);
            return route.fulfill(bytes ? { contentType: "application/octet-stream", body: Buffer.from(bytes) }
                : { contentType: "text/html", body: "<title>Linked library comparison</title>" });
        });
        await page.goto("http://localhost/");
        const edge = await page.evaluate(async ({ entries, scenario, runnerSource, exerciseSource }) => {
            const run = (0, eval)("(" + runnerSource + ")");
            const exercise = (0, eval)("(" + exerciseSource + ")");
            async function get(path) {
                const response = await fetch(path, { signal: AbortSignal.timeout(15000) });
                if (!response.ok) throw Error("Could not fetch " + path);
                return new Uint8Array(await response.arrayBuffer());
            }
            const [compiler, bundle] = await Promise.all([get("/compiler"), get("/bundle")]);
            return run(compiler, bundle, entries, exercise, scenario);
        }, { entries, scenario, runnerSource: execute.toString(), exerciseSource: exercise.toString() });
        assert.deepEqual(edge, observed);
        return { node: process.version, edge: browser.version(), observed };
    } finally {
        await browser.close();
    }
}
