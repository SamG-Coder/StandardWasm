import { createServer } from "node:http";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import assert from "node:assert/strict";
import { chromium } from "playwright-core";
import { root, run } from "./toolchain.mjs";

process.chdir(root);
const stage = "demos/stage-32-winforms-textbox";
const artifacts = "artifacts/profile";
const verify = process.argv.includes("--verify");
mkdirSync(artifacts, { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", stage + "/BrowserModel", "--", artifacts + "/Profile.wasm"]);
if (verify)
    run("dotnet", ["run", "-c", "Release", "--project", stage + "/Desktop", "--", artifacts + "/native.json"]);

const files = new Map([
    ["/", ["text/html", stage + "/web/index.html"]],
    ["/style.css", ["text/css", stage + "/web/style.css"]],
    ["/app.mjs", ["text/javascript", stage + "/web/app.mjs"]],
    ["/render.mjs", ["text/javascript", "demos/stage-14-winforms-webgpu/web/render.mjs"]],
    ["/Profile.wasm", ["application/wasm", artifacts + "/Profile.wasm"]]
]);
const server = createServer((request, response) => {
    const asset = files.get(request.url);
    if (!asset) { response.writeHead(404).end(); return; }
    response.writeHead(200, { "Content-Type": asset[0] });
    response.end(readFileSync(asset[1]));
});

await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(verify ? 0 : 5174, "127.0.0.1", resolve);
});
const url = "http://127.0.0.1:" + server.address().port;
console.log("Profile form at " + url);

if (verify) {
    let browser;
    try {
        const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
        browser = await chromium.launch({
            executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
        });
        const page = await browser.newPage({ viewport: { width: 640, height: 440 } });
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(url);
        await page.waitForFunction(() => document.querySelector("#renderer").textContent !== "Starting");
        assert.equal(await page.locator("#renderer").textContent(), "WebGPU", await page.locator("#error").textContent());

        const input = page.getByRole("textbox", { name: "Name" });
        const preview = page.locator("#preview");
        const states = [];
        async function record() {
            states.push({ input: await input.inputValue(), preview: await preview.textContent() });
        }
        const surface = page.locator("#surface");
        await record();
        const before = await surface.screenshot();
        await input.focus();
        await input.pressSequentially("Zo");
        await page.keyboard.insertText("\u00eb \ud83d\ude80!");
        await record();
        await input.evaluate(node => node.setSelectionRange(0, 3));
        await page.keyboard.insertText("Ada");
        await record();
        await input.press("End");
        await input.press("Backspace");
        await record();
        const after = await surface.screenshot();
        await page.screenshot({ path: artifacts + "/browser.png" });
        await page.getByRole("button", { name: "Clear" }).click();
        await record();

        assert.equal(await input.evaluate(node => node === document.activeElement), true);
        const native = JSON.parse(readFileSync(artifacts + "/native.json", "utf8"));
        assert.deepEqual(states, native.states);
        const surfaceBounds = await surface.boundingBox();
        const button = page.getByRole("button", { name: "Clear" });
        async function bounds(locator) {
            const box = await locator.boundingBox();
            return [box.x - surfaceBounds.x, box.y - surfaceBounds.y, box.width, box.height].map(Math.round);
        }
        const controls = {
            title: await page.locator("#title").textContent(),
            limit: await input.evaluate(node => node.maxLength),
            buttonText: await button.textContent(),
            inputBounds: await bounds(input),
            previewBounds: JSON.parse(await page.locator("#profile").getAttribute("data-preview-bounds")),
            buttonBounds: await bounds(button)
        };
        assert.deepEqual(controls, native.controls);
        assert.deepEqual(states.map(state => state.input), ["", "Zo\u00eb \ud83d\ude80!", "Ada \ud83d\ude80!", "Ada \ud83d\ude80", ""]);
        const changedPixels = await page.evaluate(async ({ before, after }) => {
            async function read(bytes) {
                const bitmap = await createImageBitmap(new Blob([new Uint8Array(bytes)], { type: "image/png" }));
                const context = new OffscreenCanvas(bitmap.width, bitmap.height).getContext("2d");
                context.drawImage(bitmap, 0, 0);
                return context.getImageData(32, 116, 356, 90).data;
            }
            const [a, b] = await Promise.all([read(before), read(after)]);
            let changed = 0;
            for (let i = 0; i < a.length; i += 4)
                if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2]) changed++;
            return changed;
        }, { before: Array.from(before), after: Array.from(after) });
        assert.ok(changedPixels > 100, "WebGPU preview pixels must change after editing.");
        assert.deepEqual(errors, []);
        const evidence = { engine: browser.version(), states, controls, changedPixels };
        writeFileSync(artifacts + "/browser.json", JSON.stringify(evidence, null, 2) + "\n");
        console.log(JSON.stringify(evidence, null, 2));
    } finally {
        try { if (browser) await browser.close(); }
        finally { await new Promise(resolve => server.close(resolve)); }
    }
}
