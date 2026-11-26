import { canvasBaseline, createTextLayer, webgpuRenderer } from "./render.mjs";

async function start() {
    const [resources, compiled] = await Promise.all([
        fetch("./resources.json").then(response => response.json()),
        WebAssembly.instantiateStreaming(fetch("./Handlers.wasm"))
    ]);
    const exports = compiled.instance.exports;
    function readText(value) {
        const count = exports.TextLength(value);
        const chars = new Array(count);
        for (let index = 0; index < count; index++)
            chars[index] = String.fromCharCode(exports.TextCodeUnit(value, index));
        return chars.join("");
    }
    const title = readText(exports.Title());
    const captions = new Map();
    const state = exports.Create(0);
    const tree = exports.BuildTree();
    const { columns, keys, kinds } = resources;
    function describe(index) {
        const field = column => exports.ControlValue(tree, index, column);
        const key = field(columns.key);
        const kind = field(columns.kind);
        if (kind !== kinds.button && kind !== kinds.label)
            throw Error("Unsupported runtime control kind " + kind);
        if (kind === kinds.button && !captions.has(key))
            captions.set(key, readText(exports.Caption(key)));
        return {
            name: key, kind: kind === kinds.button ? "button" : "label",
            text: captions.get(key) ?? "",
            x: field(columns.x), y: field(columns.y),
            width: field(columns.width), height: field(columns.height)
        };
    }
    const model = {
        width: exports.ClientWidth(tree), height: exports.ClientHeight(tree),
        capacity: exports.ControlCapacity(tree),
        controls: Array.from({ length: exports.ControlCount(tree) }, (_, index) => describe(index))
    };

    const form = document.querySelector("#form");
    const canvas = document.querySelector("#surface");
    canvas.width = model.width;
    canvas.height = model.height;
    form.style.width = model.width + "px";
    document.querySelector("#title").textContent = title;

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
    const clickBinding = exports.Bind(state);
    function activate() {
        exports.Dispatch(clickBinding, tree);
        repaint();
    }

    function place(button, c) {
        button.style.left = 100 * c.x / model.width + "%";
        button.style.top = 100 * c.y / model.height + "%";
        button.style.width = 100 * c.width / model.width + "%";
        button.style.height = 100 * c.height / model.height + "%";
    }

    function repaint() {
        const count = exports.ControlCount(tree);
        if (count > model.capacity)
            throw Error("The managed tree exceeded the renderer capacity.");
        const next = Array.from({ length: count }, (_, index) => describe(index));
        const present = new Set();
        for (const [index, control] of next.entries()) {
            const previous = model.controls[index];
            const changedSlot = previous?.name !== control.name || previous.kind !== control.kind;
            const moved = changedSlot || previous.x !== control.x || previous.y !== control.y ||
                previous.width !== control.width || previous.height !== control.height;
            if (changedSlot) {
                gpu.updateControl(index, control);
                baseline.updateControl(index, control);
            } else if (moved) {
                gpu.updateBounds(index, control);
            }
            if (control.kind !== "button") continue;
            present.add(control.name);
            texts.set(control.name, control.text);
            let button = buttons.get(control.name);
            if (!button) {
                button = document.createElement("button");
                button.type = "button";
                button.className = "native-input";
                button.textContent = control.text;
                button.addEventListener("click", activate);
                buttons.set(control.name, button);
                form.append(button);
                place(button, control);
            } else if (moved) {
                place(button, control);
            }
        }

        let restoreFocus = false;
        for (const [key, button] of buttons) {
            if (present.has(key)) continue;
            if (document.activeElement === button) restoreFocus = true;
            button.remove();
            buttons.delete(key);
            texts.delete(key);
        }
        if (restoreFocus)
            buttons.get(exports.DefaultButton(tree))?.focus();

        model.controls = next;
        form.dataset.labelLeft = String(next.find(control => control.name === keys.result).x);
        form.dataset.controlCount = String(next.length);
        result.value = readText(exports.LabelText(state));
        texts.set(keys.result, result.value);
        text.update(texts);
        gpu.upload();
        gpu.draw();
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
