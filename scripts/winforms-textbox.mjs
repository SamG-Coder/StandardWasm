import { createServer } from "node:http";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import assert from "node:assert/strict";
import { chromium } from "playwright-core";
import { root, run } from "./toolchain.mjs";
import { comparePixels, recordProfileTiming, timingOptions } from "../demos/stage-38-profile-rendering/measurements.mjs";

process.chdir(root);
const stage = "demos/stage-32-winforms-textbox";
const artifacts = "artifacts/profile";
const benchmark = process.argv.includes("--benchmark");
const verify = benchmark || process.argv.includes("--verify");
const timingOutput = "artifacts/profile-rendering";
mkdirSync(artifacts, { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", stage + "/BrowserModel", "--", artifacts + "/Profile.wasm"]);
if (verify)
    run("dotnet", ["run", "-c", "Release", "--project", stage + "/Desktop", "--", artifacts + "/native.json"]);
if (benchmark) {
    mkdirSync(timingOutput, { recursive: true });
    const { samples, warmup, repeats, values } = timingOptions;
    run("dotnet", ["run", "-c", "Release", "--no-build", "--project", stage + "/Desktop", "--",
        "--benchmark", timingOutput + "/native.json", String(samples * repeats), String(warmup), ...values]);
}

const files = new Map([
    ["/", ["text/html", stage + "/web/index.html"]],
    ["/style.css", ["text/css", stage + "/web/style.css"]],
    ["/app.mjs", ["text/javascript", stage + "/web/app.mjs"]],
    ["/profile-timing.mjs", ["text/javascript", "demos/stage-38-profile-rendering/profile-timing.mjs"]],
    ["/render.mjs", ["text/javascript", "demos/stage-14-winforms-webgpu/web/render.mjs"]],
    ["/Profile.wasm", ["application/wasm", artifacts + "/Profile.wasm"]]
]);
const server = createServer((request, response) => {
    const asset = files.get(request.url.split("?")[0]);
    if (!asset) { response.writeHead(404).end(); return; }
    const headers = { "Content-Type": asset[0] };
    if (benchmark) {
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    }
    response.writeHead(200, headers);
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
        await page.goto(benchmark ? url + "/?benchmark" : url);
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
            buttonBounds: await bounds(button),
            promptBounds: await bounds(page.locator("#prompt")),
            promptText: await page.locator("#prompt").textContent(),
            children: await page.locator("#profile").evaluate(node => Array.from(node.children)
                .filter(child => child.matches("input, output, button, label")).map(child => child.id))
        };
        assert.deepEqual(controls, native.controls);
        assert.deepEqual(states.map(state => state.input), ["", "Zo\u00eb \ud83d\ude80!", "Ada \ud83d\ude80!", "Ada \ud83d\ude80", ""]);
        const { changed: changedPixels } = await comparePixels(page, before, after, 0, [32, 116, 356, 90]);
        assert.ok(changedPixels > 100, "WebGPU preview pixels must change after editing.");
        assert.deepEqual(errors, []);
        const evidence = { engine: browser.version(), states, controls, changedPixels };
        writeFileSync(artifacts + "/browser.json", JSON.stringify(evidence, null, 2) + "\n");
        console.log(JSON.stringify(evidence, null, 2));
        if (benchmark) {
            await recordProfileTiming(page, browser.version(), timingOutput);
            assert.deepEqual(errors, []);
        }
    } finally {
        try { if (browser) await browser.close(); }
        finally { await new Promise(resolve => server.close(resolve)); }
    }
}
