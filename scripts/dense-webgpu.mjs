import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { chromium } from "playwright-core";
import { root } from "./toolchain.mjs";

process.chdir(root);
const stage = "demos/stage-17-dense-webgpu/web";
const assets = new Map([
    ["/", ["text/html", stage + "/index.html"]],
    ["/benchmark.mjs", ["text/javascript", stage + "/benchmark.mjs"]],
    ["/render.mjs", ["text/javascript", "demos/stage-14-winforms-webgpu/web/render.mjs"]]
]);
const server = createServer((req, res) => {
    const asset = assets.get(req.url);
    if (!asset) { res.writeHead(404).end(); return; }
    res.writeHead(200, { "Content-Type": asset[0] });
    res.end(readFileSync(asset[1]));
});
const verify = process.argv.includes("--verify");
await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(verify ? 0 : 5174, "127.0.0.1", resolve);
});
const url = "http://127.0.0.1:" + server.address().port;
console.log("WinForms redraw comparison at " + url);
if (verify) {
    let browser;
    try {
        const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
        browser = await chromium.launch({
            executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
        });
        const page = await browser.newPage({ viewport: { width: 2000, height: 780 } });
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("console", message => { if (message.type() === "warning") errors.push(message.text()); });
        await page.goto(url);
        await page.waitForFunction(() => typeof window.benchmark === "function");
        const dimensions = await page.locator("#gpu").evaluate(canvas => [canvas.width, canvas.height]);
        const cases = [], parity = [];
        const output = "artifacts/dense-webgpu";
        mkdirSync(output, { recursive: true });
        for (const count of [2, 128, 512, 2048]) {
            await page.evaluate(value => window.renderParity(value), count);
            const images = {};
            for (const name of ["gpu", "canvas2d"]) {
                const box = await page.locator("#" + name).boundingBox();
                assert.ok(box && box.width === dimensions[0] && box.height === dimensions[1],
                    "The canvas must be displayed at its native dimensions");
                // Use an integer CSS clip so a fractional page offset cannot add a screenshot row.
                const clip = { x: Math.round(box.x), y: Math.round(box.y), width: box.width, height: box.height };
                images[name] = await page.screenshot({ clip, scale: "css" });
            }
            const metrics = await page.evaluate(async ({ gpu, canvas2d }) => {
                async function pixels(encoded) {
                    const bitmap = await createImageBitmap(new Blob([new Uint8Array(encoded)], { type: "image/png" }));
                    const context = new OffscreenCanvas(bitmap.width, bitmap.height).getContext("2d");
                    context.drawImage(bitmap, 0, 0);
                    return { width: bitmap.width, height: bitmap.height,
                        data: context.getImageData(0, 0, bitmap.width, bitmap.height).data };
                }
                const first = await pixels(gpu), second = await pixels(canvas2d);
                if (first.width !== second.width || first.height !== second.height) throw Error("Canvas dimensions disagree");
                let outside = 0, max = 0;
                for (let i = 0; i < first.data.length; i += 4) {
                    let delta = 0;
                    for (let c = 0; c < 3; c++) delta = Math.max(delta, Math.abs(first.data[i + c] - second.data[i + c]));
                    if (delta > 1) outside++;
                    max = Math.max(max, delta);
                }
                return { pixels: first.width * first.height, outside, max };
            }, { gpu: Array.from(images.gpu), canvas2d: Array.from(images.canvas2d) });
            assert.equal(metrics.pixels, dimensions[0] * dimensions[1], "Unexpected screenshot pixel count");
            assert.equal(metrics.outside, 0, "Rendering parity failed: " + JSON.stringify(metrics));
            parity.push({ count, ...metrics });
            if (count === 2048) {
                writeFileSync(output + "/webgpu.png", images.gpu);
                writeFileSync(output + "/canvas2d.png", images.canvas2d);
            }
            for (const fraction of [0, .1, 1]) {
                const result = await page.evaluate(options => window.benchmark(options), { count, fraction });
                cases.push(result);
                console.log(count + " controls, " + fraction + " changed: WebGPU p95 " +
                    result.gpu.submitP95Ms.toFixed(3) + " ms; Canvas 2D p95 " + result.canvas2d.submitP95Ms.toFixed(3) + " ms");
            }
        }
        assert.deepEqual(errors, []);
        const drivers = process.platform === "win32"
            ? spawnSync("powershell.exe", ["-NoProfile", "-Command",
                "Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion | ConvertTo-Json -Compress"],
                { encoding: "utf8" }).stdout?.trim() : null;
        const evidence = { browser: browser.version(), drivers: drivers ? JSON.parse(drivers) : null,
            dimensions, parity, cases };
        writeFileSync(output + "/results.json", JSON.stringify(evidence, null, 2) + "\n");
        console.log("Wrote " + output + "/results.json");
    } finally {
        try { if (browser) await browser.close(); }
        finally { await new Promise(resolve => server.close(resolve)); }
    }
}
