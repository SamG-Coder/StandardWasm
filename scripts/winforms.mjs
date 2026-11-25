import { createServer } from "node:http";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { root, run } from "./toolchain.mjs";
import assert from "node:assert/strict";

process.chdir(root);
const stage = "demos/stage-14-winforms-webgpu";
const artifacts = "artifacts/winforms";
const verify = process.argv.includes("--verify");
mkdirSync(artifacts, { recursive: true });

run("dotnet", ["run", "-c", "Release", "--project", stage + "/BrowserModel", "--",
    artifacts + "/form.json", artifacts + "/Handlers.wasm"]);
if (verify)
    run("dotnet", ["run", "-c", "Release", "--project", stage + "/Desktop", "--", "--verify", artifacts + "/desktop.png"]);

const assets = new Map([
    ["/", ["text/html", join(stage, "web/index.html")]],
    ["/style.css", ["text/css", join(stage, "web/style.css")]],
    ["/app.mjs", ["text/javascript", join(stage, "web/app.mjs")]],
    ["/render.mjs", ["text/javascript", join(stage, "web/render.mjs")]],
    ["/form.json", ["application/json", join(artifacts, "form.json")]],
    ["/Handlers.wasm", ["application/wasm", join(artifacts, "Handlers.wasm")]]
]);
const server = createServer((request, response) => {
    const asset = assets.get(request.url);
    if (!asset) { response.writeHead(404).end(); return; }
    response.writeHead(200, { "Content-Type": asset[0] });
    response.end(readFileSync(asset[1]));
});
await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(verify ? 0 : 5173, "127.0.0.1", resolve);
});
const url = "http://127.0.0.1:" + server.address().port;
console.log("Counter form at " + url);
if (verify) {
    let browser;
    try {
        const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
        browser = await chromium.launch({
            executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
        });
        const page = await browser.newPage({ viewport: { width: 600, height: 400 } });
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(url);
        await page.waitForFunction(() =>
            document.querySelector("#renderer").dataset.ready === "true" || !document.querySelector("#error").hidden);
        assert.equal(await page.locator("#renderer").textContent(), "WebGPU", await page.locator("#error").textContent());
        const label = page.locator("output");
        const capture = () => page.locator("#surface").screenshot();
        assert.equal(await label.textContent(), "Count: 0");
        const before = await capture();
        await page.getByRole("button", { name: "Add one" }).click();
        assert.equal(await label.textContent(), "Count: 1");
        await page.locator("button").evaluate(button => button.blur());
        await page.keyboard.press("Enter");
        assert.equal(await label.textContent(), "Count: 2");
        const after = await capture();
        const { changed, bright } = await page.evaluate(async ({ before, after }) => {
            async function read(bytes) {
                const image = await createImageBitmap(new Blob([new Uint8Array(bytes)], { type: "image/png" }));
                const context = new OffscreenCanvas(image.width, image.height).getContext("2d");
                context.drawImage(image, 0, 0);
                return context.getImageData(48, 50, 250, 66).data;
            }
            const a = await read(before), b = await read(after);
            let changed = 0, bright = 0;
            for (let i = 0; i < a.length; i += 4) {
                if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2]) changed++;
                if (b[i] > 180 && b[i + 1] > 180 && b[i + 2] > 180) bright++;
            }
            return { changed, bright };
        }, { before: Array.from(before), after: Array.from(after) });
        assert.ok(changed > 30 && bright > 100, "The rendered count glyphs must visibly change.");
        const instances = await page.evaluate(async () => {
            const { module, instance } = await WebAssembly.instantiateStreaming(fetch("./Handlers.wasm"));
            const { Create, Bind, Dispatch, Read } = instance.exports;
            const first = Create(7);
            const second = Create(100);
            let nullTraps = false;
            try { Dispatch(null); }
            catch (error) { nullTraps = error instanceof WebAssembly.RuntimeError; }
            const click = Bind(first);
            return { first: Dispatch(click), second: Read(second),
                imports: WebAssembly.Module.imports(module).length, nullTraps };
        });
        assert.deepEqual(instances, { first: 8, second: 100, imports: 0, nullTraps: true });
        const timing = await page.evaluate(() => window.measureRendering());
        assert.deepEqual(errors, []);
        const evidence = {
            engine: browser.version(), sequence: ["Count: 0", "Count: 1", "Count: 2"],
            changedLabelPixels: changed, instances, ...timing
        };
        await page.screenshot({ path: artifacts + "/browser.png" });
        writeFileSync(artifacts + "/timing.json", JSON.stringify(evidence, null, 2) + "\n");
        console.log(JSON.stringify(evidence, null, 2));
    } finally {
        try { if (browser) await browser.close(); }
        finally { await new Promise(resolve => server.close(resolve)); }
    }
}
