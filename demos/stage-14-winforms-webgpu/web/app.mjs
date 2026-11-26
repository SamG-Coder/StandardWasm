import { canvasBaseline, createTextLayer, webgpuRenderer } from "./render.mjs";

async function start() {
    const [model, compiled] = await Promise.all([
        fetch("./form.json").then(response => response.json()),
        WebAssembly.instantiateStreaming(fetch("./Handlers.wasm"))
    ]);
    const exports = compiled.instance.exports;
    const click = exports[model.binding.export];
    if (typeof click !== "function") throw Error("The form's C# click handler was not exported.");

    const form = document.querySelector("#form");
    const canvas = document.querySelector("#surface");
    canvas.width = model.width;
    canvas.height = model.height;
    form.style.width = model.width + "px";
    document.querySelector("#title").textContent = model.title;

    const texts = new Map(model.controls.map(c => [c.name, c.text]));
    const text = createTextLayer(model);
    const gpu = await webgpuRenderer(canvas, model, text);
    const baseline = canvasBaseline(model, text);
    const status = document.querySelector("#renderer");
    status.textContent = "WebGPU";
    status.dataset.ready = "true";
    gpu.lost.then(details => report(Error("WebGPU device lost: " + details.message)));
    const buttons = new Map();
    const result = document.createElement("output");
    result.className = "sr-only";
    result.setAttribute("aria-live", "polite");
    form.append(result);
    const state = exports.Create(0);
    function repaint() {
        result.value = model.binding.prefix + exports.Read(state);
        texts.set(model.binding.label, result.value);
        text.update(texts);
        gpu.upload();
        gpu.draw();
    }

    for (const c of model.controls.filter(control => control.kind === "button")) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "native-input";
        button.textContent = c.text;
        button.style.left = 100 * c.x / model.width + "%";
        button.style.top = 100 * c.y / model.height + "%";
        button.style.width = 100 * c.width / model.width + "%";
        button.style.height = 100 * c.height / model.height + "%";
        if (c.name === model.binding.button)
            button.addEventListener("click", () => { click(state); repaint(); });
        buttons.set(c.name, button);
        form.append(button);
    }
    document.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.repeat && event.target === document.body) {
            const button = buttons.get(model.acceptButton);
            if (button) {
                event.preventDefault();
                button.click();
            }
        }
    });
    repaint();

    // Measure the main-thread draw call only with immutable geometry and cached text.
    // GPU queue completion has different semantics and is reported independently.
    window.measureRendering = async (frames = 90) => {
        function percentile(values, fraction) {
            values.sort((a, b) => a - b);
            return values[Math.ceil(fraction * values.length) - 1];
        }
        async function measure(draw) {
            const values = [];
            for (let i = -12; i < frames; i++) {
                await new Promise(requestAnimationFrame);
                const start = performance.now();
                draw();
                if (i >= 0) values.push(performance.now() - start);
            }
            return { medianMs: percentile(values, .5), p95Ms: percentile(values, .95) };
        }
        const webgpu = await measure(gpu.draw);
        const queuedAt = performance.now();
        await gpu.finished();
        const queueDrainMs = performance.now() - queuedAt;
        const canvas2d = await measure(baseline);
        return { frames, adapter: gpu.info, webgpu, canvas2d, queueDrainMs };
    };
}

function report(error) {
    const node = document.querySelector("#error");
    node.hidden = false;
    node.textContent = error.message;
    document.querySelector("#renderer").textContent = "Unavailable";
}

start().catch(report);
