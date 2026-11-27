import assert from "node:assert/strict";
import { createServer } from "node:http";
import { chromium } from "playwright-core";

async function run(compiler, bundle, cases) {
    const { instance: { exports: build } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const data = build.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) build.SetByte(data, i, bytes[i]);
        return data;
    }
    const image = upload(new Uint8Array(bundle));
    const output = build.CreateBytes(1 << 20), location = build.CreateDiagnostic();
    const methods = new Map();
    const results = [];
    for (const { method, row, arguments: args } of cases) {
        if (!methods.has(row)) {
            const entry = new Uint8Array(11);
            new DataView(entry.buffer).setUint32(0, row, true);
            entry.set([3, 0, 0, 0, 114, 117, 110], 4);
            const size = build.CompileBundleDetailed(image, upload(entry), output, location);
            if (size <= 0 || size > (1 << 20))
                throw Error(`${method} returned ${size} at ${[0, 1, 2].map(i => build.ReadDiagnostic(location, i))}`);
            const code = Uint8Array.from({ length: size }, (_, i) => build.ReadByte(output, i));
            const { instance } = await WebAssembly.instantiate(code);
            methods.set(row, instance.exports.run);
        }
        results.push({ method, result: methods.get(row)(...args) });
    }
    return results;
}

export async function verifyManagedBundle(compiler, bundle, oracle) {
    const assets = [compiler, bundle];
    const node = await run(...assets, oracle);
    const browser = await chromium.launch({ channel: "msedge" });
    try {
        const page = await browser.newPage();
        const server = createServer((req, res) => {
            if (req.url === "/") return res.end("<!doctype html><title>StandardWasm CLR comparison</title>");
            const body = assets[Number(req.url.slice(1))];
            if (!body) {
                res.writeHead(404);
                return res.end();
            }
            res.writeHead(200, { "Content-Type": "application/octet-stream", "Content-Length": body.length });
            res.end(body);
        });
        await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
        let edge;
        try {
            await page.goto(`http://127.0.0.1:${server.address().port}/`);
            edge = await page.evaluate(async ({ cases, source }) => {
                const assets = await Promise.all([0, 1].map(async i => (await fetch("/" + i)).arrayBuffer()));
                return (0, eval)("(" + source + ")")(...assets, cases);
            }, { cases: oracle, source: run.toString() });
        } finally {
            await new Promise(resolve => server.close(resolve));
        }
        assert.deepEqual(edge, node);
        assert.deepEqual(node.map(({ result }) => result), oracle.map(({ value }) => value));
        return { node: process.version, edge: browser.version(), comparisons: node.length, cases: node };
    } finally {
        await browser.close();
    }
}
