import { canvasBaseline, createTextLayer, webgpuRenderer } from "./render.mjs";

async function start() {
    const [resources, compiled] = await Promise.all([
        fetch("./resources.json").then(response => response.json()),
        WebAssembly.instantiateStreaming(fetch("./Handlers.wasm"))
    ]);
    const exports = compiled.instance.exports;
    const tree = exports.BuildTree();
    const { columns, keys, kinds } = resources;
    function describe(index) {
        const field = column => exports.ControlValue(tree, index, column);
        const key = field(columns.key);
        const kind = field(columns.kind);
        if (kind !== kinds.button && kind !== kinds.label)
            throw Error("Unsupported runtime control kind " + kind);
        return {
            name: key, kind: kind === kinds.button ? "button" : "label",
            text: key === keys.increment ? resources.increment : resources.prefix + "0",
            x: field(columns.x), y: field(columns.y),
            width: field(columns.width), height: field(columns.height)
        };
    }
    const model = {
        width: exports.ClientWidth(tree), height: exports.ClientHeight(tree),
        controls: Array.from({ length: exports.ControlCount(tree) }, (_, index) => describe(index))
    };

    const form = document.querySelector("#form");
    const canvas = document.querySelector("#surface");
    canvas.width = model.width;
    canvas.height = model.height;
    form.style.width = model.width + "px";
    document.querySelector("#title").textContent = resources.title;

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
    const clickBinding = exports.Bind(state);
    function repaint() {
        if (exports.ControlCount(tree) !== model.controls.length)
            throw Error("This renderer supports property changes on its existing controls.");
        for (const [index, control] of model.controls.entries()) {
            const updated = describe(index);
            if (updated.name !== control.name || updated.kind !== control.kind)
                throw Error("Control identity changed after startup.");
            if (updated.x !== control.x || updated.y !== control.y ||
                updated.width !== control.width || updated.height !== control.height) {
                Object.assign(control, updated);
                gpu.updateBounds(index, control);
                const button = buttons.get(control.name);
                if (button) place(button, control);
            }
        }
        form.dataset.labelLeft = String(model.controls.find(control => control.name === keys.result).x);
        form.dataset.controlCount = String(model.controls.length);
        result.value = resources.prefix + exports.Read(state);
        texts.set(keys.result, result.value);
        text.update(texts);
        gpu.upload();
        gpu.draw();
    }

    function place(button, c) {
        button.style.left = 100 * c.x / model.width + "%";
        button.style.top = 100 * c.y / model.height + "%";
        button.style.width = 100 * c.width / model.width + "%";
        button.style.height = 100 * c.height / model.height + "%";
    }

    for (const c of model.controls.filter(control => control.kind === "button")) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "native-input";
        button.textContent = c.text;
        place(button, c);
        if (c.name === keys.increment)
            button.addEventListener("click", () => { exports.Dispatch(clickBinding, tree); repaint(); });
        buttons.set(c.name, button);
        form.append(button);
    }
    document.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.repeat && event.target === document.body) {
            const button = buttons.get(exports.DefaultButton(tree));
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
        const canvas2d = await measure(baseline.draw);
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
