import { createTextLayer, webgpuRenderer } from "./render.mjs";

async function start() {
    const { instance } = await WebAssembly.instantiateStreaming(fetch("./Profile.wasm"));
    const wasm = instance.exports;
    function readText(value) {
        const units = new Array(wasm.TextLength(value));
        for (let i = 0; i < units.length; i++)
            units[i] = String.fromCharCode(wasm.TextCodeUnit(value, i));
        return units.join("");
    }

    const state = wasm.CreateState();
    const form = wasm.CreateForm(), inputModel = wasm.CreateInput();
    const preview = wasm.CreatePreview(), button = wasm.CreateClearButton();
    wasm.Initialize(state, form, inputModel, preview, button);
    const width = wasm.ClientWidth(), height = wasm.ClientHeight();
    const face = (name, kind, bound, control) => ({
        name, kind, x: bound(control, 0), y: bound(control, 1),
        width: bound(control, 2), height: bound(control, 3)
    });
    const inputBox = face(1, "input", wasm.InputBound, inputModel);
    const previewBox = face(2, "label", wasm.PreviewBound, preview);
    const clearBox = face(3, "button", wasm.ButtonBound, button);
    const model = { width, height, controls: [previewBox, clearBox] };
    const canvas = document.querySelector("#surface");
    canvas.width = width;
    canvas.height = height;
    const profile = document.querySelector("#profile");
    profile.style.width = width + "px";
    profile.dataset.previewBounds = JSON.stringify([previewBox.x, previewBox.y, previewBox.width, previewBox.height]);

    const input = document.querySelector("#name");
    input.maxLength = wasm.ReadLimit(inputModel);
    const clear = document.querySelector("#clear");
    const result = document.querySelector("#preview");
    const caption = readText(wasm.ReadButton(button));
    clear.textContent = caption;
    document.querySelector("#prompt").textContent = readText(wasm.InputLabel());
    document.querySelector("#title").textContent = readText(wasm.ReadTitle(form));
    for (const [node, box] of [[input, inputBox], [clear, clearBox]]) {
        node.style.left = 100 * box.x / width + "%";
        node.style.top = 100 * box.y / height + "%";
        node.style.width = 100 * box.width / width + "%";
        node.style.height = 100 * box.height / height + "%";
    }

    const text = createTextLayer(model);
    const gpu = await webgpuRenderer(canvas, model, text);
    const captions = new Map([[clearBox.name, caption]]);
    gpu.lost.then(details => report(Error("WebGPU device lost: " + details.message)));

    function repaint() {
        result.value = readText(wasm.ReadPreview(preview));
        captions.set(previewBox.name, result.value);
        text.update(captions);
        gpu.upload();
        gpu.draw();
    }

    function edit() {
        const value = input.value;
        // Copy UTF-16 code units so paired surrogates reach the managed string intact.
        const units = wasm.CreateChars(value.length);
        for (let i = 0; i < value.length; i++)
            wasm.SetCodeUnit(units, i, value.charCodeAt(i));
        wasm.Edit(state, inputModel, preview, units);
        repaint();
    }

    input.addEventListener("input", event => {
        if (!event.isComposing) edit();
    });
    input.addEventListener("compositionend", edit);
    clear.addEventListener("click", () => {
        wasm.Clear(state, inputModel, preview);
        input.value = readText(wasm.ReadInput(inputModel));
        repaint();
        if (wasm.TakeFocusRequest(inputModel)) input.focus();
    });
    repaint();
    document.querySelector("#renderer").textContent = "WebGPU";
}

function report(error) {
    document.querySelector("#renderer").textContent = "Unavailable";
    const message = document.querySelector("#error");
    message.textContent = error.message;
    message.hidden = false;
}

start().catch(report);
