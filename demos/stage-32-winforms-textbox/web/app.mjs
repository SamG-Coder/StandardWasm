import { createTextLayer, webgpuRenderer } from "./render.mjs";

async function start() {
    const options = new URLSearchParams(location.search);
    let instance, compiledBytes;
    if (options.has("generated")) {
        const { compileProfile } = await import("./compile-profile.mjs");
        ({ instance, bytes: compiledBytes } = await compileProfile());
    } else {
        ({ instance } = await WebAssembly.instantiateStreaming(fetch("./Profile.wasm")));
    }
    const wasm = instance.exports;
    function readText(value) {
        const units = new Array(wasm.TextLength(value));
        for (let i = 0; i < units.length; i++)
            units[i] = String.fromCharCode(wasm.TextCodeUnit(value, i));
        return units.join("");
    }

    const state = wasm.CreateState();
    const form = wasm.CreateForm(state);
    const width = wasm.ClientWidth(), height = wasm.ClientHeight();
    const input = document.querySelector("#name");
    const clear = document.querySelector("#clear");
    const result = document.querySelector("#preview");
    const profile = document.querySelector("#profile");
    const canvas = document.querySelector("#surface");
    canvas.width = width;
    canvas.height = height;
    profile.style.width = width + "px";

    const roles = new Map([
        ["name", { node: input, get: wasm.GetInput, bound: wasm.InputBound }],
        ["preview", { node: result, get: wasm.GetLabel, bound: wasm.LabelBound }],
        ["clear", { node: clear, get: wasm.GetButton, bound: wasm.ButtonBound }],
        ["prompt", { node: document.querySelector("#prompt"), get: wasm.GetLabel, bound: wasm.LabelBound }]
    ]);
    const model = { width, height, controls: [] };
    let previewControl, previewRegion;
    for (let child = wasm.FirstChild(form); child !== null; child = wasm.NextChild(child)) {
        const name = readText(wasm.ChildName(child));
        const role = roles.get(name);
        if (!role) throw Error("Unsupported profile control " + name);
        role.control = role.get(child);
        const [x, y, w, h] = [0, 1, 2, 3].map(i => role.bound(role.control, i));
        profile.append(role.node); // Use the managed insertion order in the DOM too.
        if (name === "preview" || name === "clear") {
            const visual = { name, kind: name === "clear" ? "button" : "label",
                x, y, width: w, height: h, clipText: name === "preview" };
            model.controls.push(visual);
            if (name === "preview") previewControl = visual;
        }
        if (name === "preview") {
            previewRegion = [x, y, w, h];
            profile.dataset.previewBounds = JSON.stringify(previewRegion);
        } else {
            role.node.style.left = 100 * x / width + "%";
            role.node.style.top = 100 * y / height + "%";
            role.node.style.width = 100 * w / width + "%";
            role.node.style.height = 100 * h / height + "%";
        }
    }

    const inputModel = roles.get("name").control;
    const preview = roles.get("preview").control;
    const button = roles.get("clear").control;
    const prompt = roles.get("prompt");
    input.maxLength = wasm.ReadLimit(inputModel);
    const caption = readText(wasm.ReadButton(button));
    clear.textContent = caption;
    prompt.node.textContent = readText(wasm.ReadLabel(prompt.control));
    document.querySelector("#title").textContent = readText(wasm.ReadTitle(form));

    const text = createTextLayer(model);
    const gpu = await webgpuRenderer(canvas, model, text);
    const captions = new Map([["clear", caption]]);
    gpu.lost.then(details => report(Error("WebGPU device lost: " + details.message)));

    function repaint(previewOnly = false) {
        result.value = readText(wasm.ReadLabel(preview));
        captions.set("preview", result.value);
        if (previewOnly) text.updateClippedCaption(previewControl, result.value);
        else text.update(captions);
        gpu.upload(previewOnly ? previewRegion : undefined);
        gpu.draw();
    }

    function setManagedText(value) {
        // Copy UTF-16 code units so paired surrogates reach the managed string intact.
        const units = wasm.CreateChars(value.length);
        for (let i = 0; i < value.length; i++)
            wasm.SetCodeUnit(units, i, value.charCodeAt(i));
        wasm.Edit(inputModel, units);
    }

    function edit() {
        setManagedText(input.value);
        repaint(true);
    }

    input.addEventListener("input", event => {
        if (!event.isComposing) edit();
    });
    input.addEventListener("compositionend", edit);
    clear.addEventListener("click", () => {
        wasm.Click(button);
        input.value = readText(wasm.ReadInput(inputModel));
        repaint(true);
        if (wasm.TakeFocusRequest(inputModel)) input.focus();
    });
    if (options.has("benchmark")) {
        const { prepareProfileTiming } = await import("./profile-timing.mjs");
        window.profileTiming = prepareProfileTiming({
            canvas, model, text, gpu, captions, setManagedText,
            previewRegion: options.has("regional") ? previewRegion : undefined,
            previewControl: options.has("region-redraw") ? previewControl : undefined,
            readPreview: () => readText(wasm.ReadLabel(preview))
        });
    }
    repaint();
    document.querySelector("#renderer").textContent = "WebGPU";
    return compiledBytes;
}

function report(error) {
    document.querySelector("#renderer").textContent = "Unavailable";
    const message = document.querySelector("#error");
    message.textContent = error.message;
    message.hidden = false;
}

export const ready = start().catch(report);
