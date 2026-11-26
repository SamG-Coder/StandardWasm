import { createServer } from "node:http";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import assert from "node:assert/strict";
import { chromium } from "playwright-core";
import { root, run } from "./toolchain.mjs";
import { comparePixels, recordProfileTiming, timingOptions } from "../demos/stage-38-profile-rendering/measurements.mjs";

process.chdir(root);
const stage = "demos/stage-32-winforms-textbox";
const generated = process.argv.includes("--generated");
const compiler = "artifacts/StandardWasm.Compiler.wasm";
const artifacts = generated ? "artifacts/profile-generated" : "artifacts/profile";
const benchmark = process.argv.includes("--benchmark");
const regional = process.argv.includes("--regional");
const verify = benchmark || process.argv.includes("--verify");
const timingOutput = regional ? "artifacts/profile-text-uploads" : "artifacts/profile-rendering";
mkdirSync(artifacts, { recursive: true });
const browserModel = ["run", "-c", "Release", "--project", stage + "/BrowserModel", "--"];
const reference = artifacts + "/Profile.wasm";
if (generated) {
    if (!existsSync(compiler)) throw Error("Generate the portable compiler with npm test first.");
    run("dotnet", [...browserModel, "--inputs", artifacts, ...(verify ? [reference] : [])]);
} else {
    run("dotnet", [...browserModel, reference]);
}
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
    ["/render.mjs", ["text/javascript", "demos/stage-14-winforms-webgpu/web/render.mjs"]]
]);
if (generated) {
    files.set("/compile-profile.mjs", ["text/javascript", "demos/stage-42-generated-profile/compile-profile.mjs"]);
    files.set("/StandardWasm.Compiler.wasm", ["application/wasm", compiler]);
    files.set("/Profile.dll", ["application/octet-stream", artifacts + "/Profile.dll"]);
    files.set("/Profile.exports", ["application/octet-stream", artifacts + "/Profile.exports"]);
} else {
    files.set("/Profile.wasm", ["application/wasm", reference]);
}
const server = createServer((request, response) => {
    const asset = files.get(request.url.split("?")[0]);
    if (!asset) { response.writeHead(404).end(); return; }
    response.writeHead(200, { "Content-Type": asset[0] });
    response.end(readFileSync(asset[1]));
});

await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(verify ? 0 : 5174, "127.0.0.1", resolve);
});
const url = new URL("http://127.0.0.1:" + server.address().port);
if (generated) url.searchParams.set("generated", "");
if (benchmark) url.searchParams.set("benchmark", "");
if (regional) url.searchParams.set("regional", "");
console.log("Profile form at " + url.href);

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
        await page.goto(url.href);
        await page.waitForFunction(() => document.querySelector("#renderer").textContent !== "Starting");
        assert.equal(await page.locator("#renderer").textContent(), "WebGPU", await page.locator("#error").textContent());
        let portable;
        if (generated) {
            const output = Buffer.from(await page.evaluate(async () => {
                const { ready } = await import("./app.mjs");
                return Array.from(await ready);
            }));
            const expected = readFileSync(reference);
            assert.ok(output.equals(expected), "The browser compiler output must match the CLR output byte for byte.");
            writeFileSync(artifacts + "/Profile.browser.wasm", output);
            const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
            portable = { bytes: output.length, sha256: sha256(output), compilerSha256: sha256(readFileSync(compiler)) };
        }

        const input = page.getByRole("textbox", { name: "Name" });
        const preview = page.locator("#preview");
        const states = [];
        async function record() {
            states.push({ input: await input.inputValue(), preview: await preview.textContent() });
        }
        const surface = page.locator("#surface");
        await record();
        // Exclude DOM overlays and rounded clipping from GPU pixel comparisons.
        const captureSurface = () => surface.screenshot({
            style: regional ? "#profile > :not(canvas) { opacity: 0 !important; } " +
                "main { border-radius: 0 !important; }" : undefined
        });
        const before = await captureSurface();
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
        const after = await captureSurface();
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
        if (regional) {
            const [x, y, width, height] = controls.previewBounds;
            const w = Math.round(surfaceBounds.width), h = Math.round(surfaceBounds.height);
            const outside = [[0, 0, w, y], [0, y + height, w, h - y - height],
                [0, y, x, height], [x + width, y, w - x - width, height]];
            for (const area of outside) {
                const { changed } = await comparePixels(page, before, after, 0, area);
                assert.equal(changed, 0, "Editing must leave pixels outside the preview intact: " + JSON.stringify({ area, bounds: controls.previewBounds, surfaceBounds }));
            }
            const cleared = await captureSurface();
            const { changed } = await comparePixels(page, before, cleared, 0);
            assert.equal(changed, 0, "Clearing must fully restore the initial frame.");
        }
        assert.deepEqual(errors, []);
        const evidence = { engine: browser.version(), states, controls, changedPixels, portable };
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
