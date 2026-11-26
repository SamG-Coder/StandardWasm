import { canvasBaseline } from "./render.mjs";

const frame = () => new Promise(requestAnimationFrame);

export function prepareProfileTiming({ canvas, model, text, gpu, captions, setManagedText, readPreview, previewRegion }) {
    const comparison = canvas.cloneNode(false);
    const canvas2d = canvasBaseline(model, text, comparison);

    function select(key) {
        if (key !== "canvas2d" && comparison.isConnected) comparison.replaceWith(canvas);
        if (key === "canvas2d" && canvas.isConnected) canvas.replaceWith(comparison);
        return key === "canvas2d" ? canvas2d : gpu;
    }

    function update(value) {
        setManagedText(value);
        return readPreview();
    }

    function draw(key, renderer, preview) {
        captions.set("preview", preview);
        text.update(captions);
        if (key !== "canvas2d") renderer.upload(key === "webgpuRegion" ? previewRegion : undefined);
        renderer.draw();
    }

    return {
        async sample({ values, samples, warmup, repeats }) {
            const totals = {
                webgpu: { updateMs: [], submitMs: [], queueDrainMs: [] },
                canvas2d: { updateMs: [], submitMs: [] }
            };
            if (previewRegion) totals.webgpuRegion = { updateMs: [], submitMs: [], queueDrainMs: [] };
            const renderers = Object.keys(totals);
            for (let repeat = 0; repeat < repeats; repeat++) {
                const first = repeat % renderers.length;
                const order = [...renderers.slice(first), ...renderers.slice(0, first)];
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
                    if (key !== "canvas2d") {
                        const start = performance.now();
                        await gpu.finished();
                        totals[key].queueDrainMs.push(performance.now() - start);
                    }
                }
            }
            return { adapter: gpu.info, totals, width: model.width, height: model.height, previewRegion };
        },
        async render(key, value) {
            const renderer = select(key);
            const preview = update(value);
            await frame();
            draw(key, renderer, preview);
            if (key !== "canvas2d") await gpu.finished();
            await frame();
            return preview;
        }
    };
}
