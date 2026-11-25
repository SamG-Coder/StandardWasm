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
    artifacts + "/resources.json", artifacts + "/Handlers.wasm"]);
if (verify)
    run("dotnet", ["run", "-c", "Release", "--project", stage + "/Desktop", "--", "--verify", artifacts + "/desktop.png"]);

const assets = new Map([
    ["/", ["text/html", join(stage, "web/index.html")]],
    ["/style.css", ["text/css", join(stage, "web/style.css")]],
    ["/app.mjs", ["text/javascript", join(stage, "web/app.mjs")]],
    ["/render.mjs", ["text/javascript", join(stage, "web/render.mjs")]],
    ["/resources.json", ["application/json", join(artifacts, "resources.json")]],
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
        const page = await browser.newPage({ viewport: { width: 760, height: 500 } });
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(url);
        await page.waitForFunction(() =>
            document.querySelector("#renderer").dataset.ready === "true" || !document.querySelector("#error").hidden);
        assert.equal(await page.locator("#renderer").textContent(), "WebGPU", await page.locator("#error").textContent());
        assert.equal(await page.locator("h1").textContent(), "Counter");
        assert.deepEqual(Object.keys(JSON.parse(readFileSync(join(artifacts, "resources.json"), "utf8"))).sort(),
            ["columns", "keys", "kinds"]);
        const label = page.locator("output");
        const form = page.locator("#form");
        const labelLeft = async () => Number(await form.getAttribute("data-label-left"));
        assert.equal(await form.getAttribute("data-control-count"), "2");
        assert.deepEqual(await page.locator("#surface").evaluate(canvas => [canvas.width, canvas.height]), [420, 240]);
        const positions = [], sequence = [], controlCounts = [];
        async function record() {
            positions.push(await labelLeft());
            sequence.push(await label.textContent());
            controlCounts.push(Number(await form.getAttribute("data-control-count")));
        }
        const capture = () => page.locator("#surface").screenshot();
        const original = page.getByRole("button", { name: "Add one", exact: true });
        const additional = page.getByRole("button", { name: "Add one again", exact: true });
        const originalNode = await original.elementHandle();
        await record();
        assert.equal(await additional.count(), 0);
        const before = await capture();
        await original.click();
        await record();
        assert.equal(await additional.count(), 1);
        const canvasBox = await page.locator("#surface").boundingBox();
        const extraBox = await additional.boundingBox();
        const dynamicBounds = [extraBox.x - canvasBox.x, extraBox.y - canvasBox.y, extraBox.width, extraBox.height]
            .map(Math.round);
        const moved = await capture();
        await page.screenshot({ path: artifacts + "/browser-moved.png" });

        await additional.focus();
        await page.keyboard.press("Enter");
        await record();
        assert.equal(await additional.count(), 0);
        assert.equal(await original.evaluate(node => node === document.activeElement), true);
        const after = await capture();

        await original.click();
        await record();
        await additional.click();
        await record();
        assert.equal(await additional.count(), 0);
        assert.equal(await original.evaluate((node, retained) => node === retained && node === document.activeElement,
            originalNode), true);
        // The removed control's former screen rectangle must no longer dispatch.
        await page.mouse.click(canvasBox.x + 250, canvasBox.y + 175);
        assert.equal(await label.textContent(), "Count: 4");

        const desktop = JSON.parse(readFileSync(join(artifacts, "desktop.json"), "utf8"));
        assert.deepEqual({ sequence, labelLeft: positions, controlCounts, dynamicBounds }, {
            sequence: desktop.sequence, labelLeft: desktop.labelLeft, controlCounts: desktop.controlCounts,
            dynamicBounds: desktop.dynamicBounds
        });
        const { changed, bright, movedEdge, inserted } = await page.evaluate(async ({ before, moved, after }) => {
            async function read(bytes, x, y, width, height) {
                const image = await createImageBitmap(new Blob([new Uint8Array(bytes)], { type: "image/png" }));
                const context = new OffscreenCanvas(image.width, image.height).getContext("2d");
                context.drawImage(image, 0, 0);
                return context.getImageData(x, y, width, height).data;
            }
            const a = await read(before, 37, 50, 250, 66);
            const m = await read(moved, 37, 50, 250, 66);
            const b = await read(after, 37, 50, 250, 66);
            const absent = await read(before, 204, 151, 184, 48);
            const added = await read(moved, 204, 151, 184, 48);
            const removed = await read(after, 204, 151, 184, 48);
            let changed = 0, bright = 0, movedEdge = 0;
            for (let i = 0; i < a.length; i += 4) {
                if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2]) changed++;
                if ((i / 4) % 250 < 15 && (a[i] !== m[i] || a[i + 1] !== m[i + 1] || a[i + 2] !== m[i + 2])) movedEdge++;
                if (b[i] > 180 && b[i + 1] > 180 && b[i + 2] > 180) bright++;
            }
            let inserted = 0;
            for (let i = 0; i < absent.length; i += 4)
                if (absent[i + 1] !== added[i + 1] && absent[i + 1] === removed[i + 1]) inserted++;
            return { changed, bright, movedEdge, inserted };
        }, { before: Array.from(before), moved: Array.from(moved), after: Array.from(after) });
        assert.ok(changed > 30 && bright > 100, "The rendered count glyphs must visibly change.");
        assert.ok(movedEdge > 300, "The label panel must visibly move with its managed bounds.");
        assert.ok(inserted > 5000, "The dynamic control must appear and clear its WebGPU rectangle.");
        const instances = await page.evaluate(async () => {
            const { module, instance } = await WebAssembly.instantiateStreaming(fetch("./Handlers.wasm"));
            const { Create, Bind, Dispatch, Read, BuildTree } = instance.exports;
            const tree = BuildTree();
            const first = Create(7);
            const second = Create(100);
            let nullTraps = false;
            try { Dispatch(null, tree); }
            catch (error) { nullTraps = error instanceof WebAssembly.RuntimeError; }
            const click = Bind(first);
            return { first: Dispatch(click, tree), second: Read(second),
                imports: WebAssembly.Module.imports(module).length, nullTraps };
        });
        assert.deepEqual(instances, { first: 8, second: 100, imports: 0, nullTraps: true });
        const resized = [];
        async function recordResize() {
            const client = await page.locator("#surface").evaluate(canvas => [canvas.width, canvas.height]);
            const surface = await page.locator("#surface").boundingBox();
            async function relative(locator) {
                const bounds = await locator.boundingBox();
                return [bounds.x - surface.x, bounds.y - surface.y, bounds.width, bounds.height].map(Math.round);
            }
            resized.push({
                client,
                label: JSON.parse(await form.getAttribute("data-label-bounds")),
                increment: await relative(original),
                additional: await additional.count() ? await relative(additional) : [],
                caption: await label.textContent()
            });
        }
        async function waitForSize(width, height) {
            await page.waitForFunction(([width, height]) => {
                const canvas = document.querySelector("#surface");
                return canvas.width === width && canvas.height === height;
            }, [width, height]);
        }
        async function setSize(width, height) {
            await form.evaluate((node, [width, height]) => {
                node.style.width = width + "px";
                node.style.height = height + "px";
            }, [width, height]);
            await waitForSize(width, height);
        }

        // Drag Chromium's actual resize handle to grow the form.
        const start = await form.boundingBox();
        await page.mouse.move(start.x + start.width - 4, start.y + start.height - 4);
        await page.mouse.down();
        await page.mouse.move(start.x + start.width + 96, start.y + start.height + 56, { steps: 8 });
        await page.mouse.up();
        await waitForSize(520, 300);
        await recordResize();
        const expanded = await page.locator("#surface").boundingBox();
        await page.mouse.click(expanded.x + 100, expanded.y + 175);
        assert.equal(await label.textContent(), "Count: 4");
        await original.click();
        await recordResize();
        const expandedPng = await capture();
        const pixels = await page.evaluate(async bytes => {
            const image = await createImageBitmap(new Blob([new Uint8Array(bytes)], { type: "image/png" }));
            const paint = new OffscreenCanvas(image.width, image.height).getContext("2d");
            paint.drawImage(image, 0, 0);
            return [[80, 175], [40, 215], [310, 215], [500, 45], [510, 45]].map(([x, y]) => paint.getImageData(x, y, 1, 1).data[1]);
        }, Array.from(expandedPng));
        assert.ok(pixels[0] < 60 && pixels[1] > 180 && pixels[2] > 180, "WebGPU must draw the buttons at the new bounds.");
        assert.ok(pixels[3] > pixels[4], "WebGPU must stretch the label panel to its new width.");
        await page.screenshot({ path: artifacts + "/browser-resized.png" });
        const expandedTiming = await page.evaluate(() => window.measureRendering(60));
        const resizedButton = await additional.elementHandle();
        await additional.focus();
        await setSize(480, 280);
        assert.equal(await additional.evaluate((node, retained) =>
            node === retained && node === document.activeElement, resizedButton), true);
        await recordResize();
        await page.keyboard.press("Enter");
        assert.equal(await additional.count(), 0);
        assert.equal(await original.evaluate((node, retained) =>
            node === retained && node === document.activeElement, originalNode), true);
        await recordResize();
        await setSize(420, 240);
        await recordResize();
        assert.deepEqual(resized, desktop.resized);

        const timing = await page.evaluate(() => window.measureRendering());
        assert.deepEqual(errors, []);
        const evidence = {
            engine: browser.version(), sequence, controlCounts, dynamicBounds,
            labelLeft: positions, movedEdgePixels: movedEdge, changedLabelPixels: changed, insertedPixels: inserted, instances, resized, expandedTiming, ...timing
        };
        await page.screenshot({ path: artifacts + "/browser.png" });
        writeFileSync(artifacts + "/timing.json", JSON.stringify(evidence, null, 2) + "\n");
        console.log(JSON.stringify(evidence, null, 2));
    } finally {
        try { if (browser) await browser.close(); }
        finally { await new Promise(resolve => server.close(resolve)); }
    }
}
