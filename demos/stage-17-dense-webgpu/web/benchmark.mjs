import { canvasBaseline, createTextLayer, webgpuRenderer } from "./render.mjs";

const width = 960, height = 600;
let scene;
let adapter;
const gpuCanvas = document.querySelector("#gpu");
const baselineCanvas = document.querySelector("#canvas2d");

function model(count) {
    const columns = Math.ceil(Math.sqrt(count * width / height));
    const rows = Math.ceil(count / columns);
    const cellWidth = Math.floor(width / columns);
    const cellHeight = Math.floor(height / rows);
    return {
        width, height,
        controls: Array.from({ length: count }, (_, i) => ({
            name: String(i), kind: i % 4 === 0 ? "button" : "label",
            x: i % columns * cellWidth + 2, y: Math.floor(i / columns) * cellHeight + 2,
            width: cellWidth - 4, height: cellHeight - 4,
            fontSize: Math.min(16, Math.floor(cellHeight * .5)), textInset: 3
        }))
    };
}

async function prepare(count) {
    if (scene?.count === count) return scene;
    if (scene) {
        await scene.gpu.finished();
        scene.gpu.dispose();
    }
    const form = model(count);
    const text = createTextLayer(form);
    text.update(new Map(form.controls.map(c => [c.name, "A" + c.name])));
    const gpu = await webgpuRenderer(gpuCanvas, form, text);
    adapter = gpu.info;
    gpu.upload();
    scene = { count, gpu, canvas: canvasBaseline(form, text, baselineCanvas) };
    return scene;
}

function percentile(values, fraction) {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.ceil(sorted.length * fraction) - 1];
}

function summarize(submit, frame) {
    return {
        submitMedianMs: percentile(submit, .5), submitP95Ms: percentile(submit, .95),
        frameMedianMs: percentile(frame, .5), frameP95Ms: percentile(frame, .95)
    };
}

async function sample(renderer, count, changed, frames, warmup) {
    renderer.update(0, count, false);
    const submit = [], frame = [];
    let previous = 0;
    for (let index = -warmup; index < frames; index++) {
        const now = await new Promise(requestAnimationFrame);
        if (index >= 0 && previous) frame.push(now - previous);
        previous = now;
        const start = performance.now();
        if (changed) renderer.update(0, changed, (index & 1) === 0);
        renderer.draw();
        if (index >= 0) submit.push(performance.now() - start);
    }
    return { submit, frame };
}

window.benchmark = async ({ count, fraction, frames = 32, warmup = 8, repeats = 3 }) => {
    const { gpu, canvas } = await prepare(count);
    const changed = Math.round(count * fraction);
    const queueDrainMs = [];
    const totals = { gpu: { submit: [], frame: [] }, canvas: { submit: [], frame: [] } };
    for (let repeat = 0; repeat < repeats; repeat++) {
        const order = repeat % 2 === 0 ? [["gpu", gpu], ["canvas", canvas]] : [["canvas", canvas], ["gpu", gpu]];
        for (const [key, renderer] of order) {
            const values = await sample(renderer, count, changed, frames, warmup);
            totals[key].submit.push(...values.submit);
            totals[key].frame.push(...values.frame);
            if (key === "gpu") {
                const start = performance.now();
                await gpu.finished();
                queueDrainMs.push(performance.now() - start);
            }
        }
    }
    return { count, changed, fraction, frames, warmup, repeats, adapter,
        gpu: { ...summarize(totals.gpu.submit, totals.gpu.frame), queueDrainMs },
        canvas2d: summarize(totals.canvas.submit, totals.canvas.frame) };
};

window.renderParity = async count => {
    const { gpu, canvas } = await prepare(count);
    const changed = Math.max(1, Math.round(count * .1));
    for (const renderer of [gpu, canvas]) {
        renderer.update(0, count, false);
        renderer.update(0, changed, true);
        renderer.draw();
    }
    await gpu.finished();
    await new Promise(requestAnimationFrame);
};

document.querySelector("#controls").addEventListener("submit", async event => {
    event.preventDefault();
    const button = event.currentTarget.querySelector("button");
    const result = document.querySelector("#result");
    const status = document.querySelector("#status");
    button.disabled = true;
    status.textContent = "Measuring both renderers";
    try {
        const evidence = await window.benchmark({
            count: Number(document.querySelector("#count").value),
            fraction: Number(document.querySelector("#change").value)
        });
        result.textContent = JSON.stringify(evidence, null, 2);
        status.textContent = "Complete";
    } catch (error) {
        status.textContent = error.message;
    } finally {
        button.disabled = false;
    }
});
