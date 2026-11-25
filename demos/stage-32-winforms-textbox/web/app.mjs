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

    const state = wasm.Create();
    const width = wasm.ClientWidth(), height = wasm.ClientHeight();
    const face = (name, kind) => ({
        name, kind, x: wasm.Left(name), y: wasm.Top(name),
        width: wasm.Width(name), height: wasm.Height(name)
    });
    const inputBox = face(1, "input"), previewBox = face(2, "label"), clearBox = face(3, "button");
    const model = { width, height, controls: [previewBox, clearBox] };
    const canvas = document.querySelector("#surface");
    canvas.width = width;
    canvas.height = height;
    document.querySelector("#profile").style.width = width + "px";

    const input = document.querySelector("#name");
    input.maxLength = wasm.InputLimit();
    const clear = document.querySelector("#clear");
    const result = document.querySelector("#preview");
    const caption = readText(wasm.ClearLabel());
    clear.textContent = caption;
    document.querySelector("#prompt").textContent = readText(wasm.InputLabel());
    document.querySelector("#title").textContent = readText(wasm.Title());
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
        result.value = readText(wasm.Preview(state));
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
        wasm.EditChars(state, units);
        repaint();
    }

    input.addEventListener("input", event => {
        if (!event.isComposing) edit();
    });
    input.addEventListener("compositionend", edit);
    clear.addEventListener("click", () => {
        input.value = "";
        edit();
        input.focus();
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
