import { canvasBaseline } from "./render.mjs";

const frame = () => new Promise(requestAnimationFrame);

function observedClockStep() {
    let previous = performance.now();
    let step = Infinity;
    let ticks = 0;
    // Observe positive changes, since several reads can land on the same tick.
    for (let reads = 0; reads < 100_000 && ticks < 64; reads++) {
        const current = performance.now();
        if (current <= previous) continue;
        step = Math.min(step, current - previous);
        previous = current;
        ticks++;
    }
    if (!Number.isFinite(step)) throw Error("Browser clock did not advance during sampling");
    return step;
}

export function prepareProfileTiming({ canvas, model, text, gpu, captions, setManagedText, readPreview }) {
    const comparison = canvas.cloneNode(false);
    const canvas2d = canvasBaseline(model, text, comparison);

    function select(key) {
        if (key === "webgpu" && comparison.isConnected) comparison.replaceWith(canvas);
        if (key === "canvas2d" && canvas.isConnected) canvas.replaceWith(comparison);
        return key === "webgpu" ? gpu : canvas2d;
    }

    function update(value) {
        setManagedText(value);
        return readPreview();
    }

    function draw(key, renderer, preview) {
        captions.set("preview", preview);
        text.update(captions);
        if (key === "webgpu") renderer.upload();
        renderer.draw();
    }

    return {
        async sample({ values, samples, warmup, repeats }) {
            if (!globalThis.crossOriginIsolated)
                throw Error("Profile timings require a cross-origin isolated page");
            const clock = { crossOriginIsolated: globalThis.crossOriginIsolated, observedStepMs: observedClockStep() };
            const totals = {
                webgpu: { updateMs: [], submitMs: [] },
                canvas2d: { updateMs: [], submitMs: [] }
            };
            for (let repeat = 0; repeat < repeats; repeat++) {
                const order = repeat % 2 === 0 ? ["webgpu", "canvas2d"] : ["canvas2d", "webgpu"];
                for (const key of order) {
                    const renderer = select(key);
                    for (let index = 0; index < warmup + samples; index++) {
                        const value = values[index % values.length];
                        await frame();
                        const start = performance.now();
                        const preview = update(value);
                        const afterUpdate = performance.now();
                        draw(key, renderer, preview);
                        const afterDraw = performance.now();
                        if (preview !== "Hello, " + value)
                            throw Error("Managed TextChanged did not update the preview");
                        if (index >= warmup) {
                            totals[key].updateMs.push(afterUpdate - start);
                            totals[key].submitMs.push(afterDraw - afterUpdate);
                        }
                    }
                    // Finish queued GPU work before switching renderers.
                    if (key === "webgpu") await gpu.finished();
                }
            }
            return { adapter: gpu.info, clock, totals, width: model.width, height: model.height };
        },
        async render(key, value) {
            const renderer = select(key);
            const preview = update(value);
            await frame();
            draw(key, renderer, preview);
            if (key === "webgpu") await gpu.finished();
            await frame();
            return preview;
        }
    };
}
