const tones = {
    background: [0.078, 0.113, 0.180],
    panel: [0.117, 0.166, 0.255],
    button: [0.384, 0.855, 0.906],
    active: [0.961, 0.702, 0.322]
};

function rgb(color) {
    return "rgb(" + color.map(c => Math.round(c * 255)).join(",") + ")";
}

export function createTextLayer(model) {
    const canvas = new OffscreenCanvas(model.width, model.height);
    const paint = canvas.getContext("2d");
    return {
        canvas,
        update(texts) {
            paint.clearRect(0, 0, model.width, model.height);
            for (const control of model.controls) {
                const button = control.kind === "button";
                const fontSize = control.fontSize ?? (button ? 16 : 34);
                const inset = control.textInset ?? (button ? 6 : 20);
                paint.font = "700 " + fontSize + "px system-ui";
                paint.fillStyle = button ? "#10212b" : "#f1f6ff";
                paint.textAlign = button ? "center" : "left";
                paint.textBaseline = "middle";
                paint.fillText(texts.get(control.name),
                    control.x + (button ? control.width * 0.5 : inset),
                    control.y + control.height * 0.5,
                    control.width - inset * 2);
            }
        }
    };
}

function controlTone(control, active) {
    return active ? tones.active : control.kind === "button" ? tones.button : tones.panel;
}

export function canvasBaseline(model, text, canvas = new OffscreenCanvas(model.width, model.height)) {
    const context = canvas.getContext("2d", { alpha: false });
    const background = rgb(tones.background);
    const initialColors = model.controls.map(control => rgb(controlTone(control, false)));
    const activeColor = rgb(tones.active);
    const states = new Uint8Array(model.controls.length);
    return {
        update(start, count, active) {
            states.fill(active ? 1 : 0, start, start + count);
        },
        draw() {
            context.fillStyle = background;
            context.fillRect(0, 0, model.width, model.height);
            let previous = background;
            for (let i = 0; i < model.controls.length; i++) {
                const c = model.controls[i];
                const color = states[i] ? activeColor : initialColors[i];
                if (color !== previous) {
                    context.fillStyle = color;
                    previous = color;
                }
                context.fillRect(c.x, c.y, c.width, c.height);
            }
            context.drawImage(text.canvas, 0, 0);
        }
    };
}

const fills = /* wgsl */ `
struct Vertex {
    @builtin(position) position: vec4f,
    @location(0) color: vec4f,
};
@vertex fn vertexMain(@builtin(vertex_index) index: u32,
                      @location(0) rect: vec4f, @location(1) color: vec4f) -> Vertex {
    let uv = vec2f(f32(index & 1u), f32(index >> 1u));
    return Vertex(vec4f(rect.xy + uv * rect.zw, 0, 1), color);
}
@fragment fn fragmentMain(in: Vertex) -> @location(0) vec4f {
    return in.color;
}
`;

const glyphs = /* wgsl */ `
struct Vertex { @builtin(position) position: vec4f, @location(0) uv: vec2f };
@group(0) @binding(0) var glyphSampler: sampler;
@group(0) @binding(1) var glyphs: texture_2d<f32>;
@vertex fn vertexMain(@builtin(vertex_index) index: u32) -> Vertex {
    let uv = vec2f(f32((index << 1u) & 2u), f32(index & 2u));
    return Vertex(vec4f(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0, 1), uv);
}
@fragment fn fragmentMain(in: Vertex) -> @location(0) vec4f {
    return textureSample(glyphs, glyphSampler, in.uv);
}
`;

export async function webgpuRenderer(canvas, model, text) {
    if (!navigator.gpu) throw Error("WebGPU requires a supported browser and a secure origin.");
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) throw Error("No WebGPU adapter is available.");
    const device = await adapter.requestDevice();
    const context = canvas.getContext("webgpu");
    if (!context) throw Error("The WebGPU canvas is unavailable.");
    const format = navigator.gpu.getPreferredCanvasFormat();
    context.configure({ device, format, alphaMode: "opaque" });

    const rectangles = new Float32Array(model.controls.length * 4);
    const initialColors = new Uint8Array(model.controls.length * 4);
    const activeColors = new Uint8Array(model.controls.length * 4);

    const rgba = tone => [...tone.map(value => Math.round(value * 255)), 255];
    const active = rgba(tones.active);
    for (const [i, control] of model.controls.entries()) {
        initialColors.set(rgba(controlTone(control, false)), i * 4);
        activeColors.set(active, i * 4);
    }

    function encodeBounds(i, c) {
        const left = 2 * c.x / model.width - 1;
        const top = 1 - 2 * c.y / model.height;
        rectangles.set([left, top, 2 * c.width / model.width, -2 * c.height / model.height], i * 4);
    }
    for (const [i, control] of model.controls.entries()) encodeBounds(i, control);
    const geometryBuffer = device.createBuffer({ size: rectangles.byteLength,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST });
    const colorBuffer = device.createBuffer({ size: initialColors.byteLength,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST });
    device.queue.writeBuffer(geometryBuffer, 0, rectangles);
    device.queue.writeBuffer(colorBuffer, 0, initialColors);
    const fillShader = device.createShaderModule({ code: fills });
    const glyphShader = device.createShaderModule({ code: glyphs });
    const fillPipeline = await device.createRenderPipelineAsync({
        layout: "auto",
        vertex: {
            module: fillShader, entryPoint: "vertexMain",
            buffers: [
                { arrayStride: 16, stepMode: "instance",
                    attributes: [{ shaderLocation: 0, offset: 0, format: "float32x4" }] },
                { arrayStride: 4, stepMode: "instance",
                    attributes: [{ shaderLocation: 1, offset: 0, format: "unorm8x4" }] }
            ]
        },
        primitive: { topology: "triangle-strip" },
        fragment: { module: fillShader, entryPoint: "fragmentMain", targets: [{ format }] }
    });
    const textPipeline = await device.createRenderPipelineAsync({
        layout: "auto",
        vertex: { module: glyphShader, entryPoint: "vertexMain" },
        fragment: { module: glyphShader, entryPoint: "fragmentMain", targets: [{ format, blend: {
            color: { srcFactor: "src-alpha", dstFactor: "one-minus-src-alpha" },
            alpha: { srcFactor: "one", dstFactor: "one-minus-src-alpha" }
        } }] }
    });
    const extent = [model.width, model.height];
    const texture = device.createTexture({
        size: extent, format: "rgba8unorm",
        usage: GPUTextureUsage.COPY_DST | GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.RENDER_ATTACHMENT
    });
    const bindings = device.createBindGroup({
        layout: textPipeline.getBindGroupLayout(0),
        entries: [
            { binding: 0, resource: device.createSampler({ magFilter: "linear", minFilter: "linear" }) },
            { binding: 1, resource: texture.createView() }
        ]
    });

    const [r, g, b] = tones.background;
    return {
        info: { vendor: adapter.info.vendor, architecture: adapter.info.architecture,
            device: adapter.info.device, description: adapter.info.description,
            fallback: adapter.info.isFallbackAdapter },
        finished: () => device.queue.onSubmittedWorkDone(),
        lost: device.lost,
        dispose() {
            context.unconfigure();
            device.destroy();
        },
        updateBounds(index, control) {
            encodeBounds(index, control);
            device.queue.writeBuffer(geometryBuffer, index * 16, rectangles, index * 4, 4);
        },
        update(start, count, active) {
            if (count === 0) return;
            const bytes = active ? activeColors : initialColors;
            device.queue.writeBuffer(colorBuffer, start * 4, bytes, start * 4, count * 4);
        },
        upload() {
            device.queue.copyExternalImageToTexture({ source: text.canvas }, { texture }, extent);
        },
        draw() {
            const encoder = device.createCommandEncoder();
            const pass = encoder.beginRenderPass({ colorAttachments: [{
                view: context.getCurrentTexture().createView(), clearValue: { r, g, b, a: 1 },
                loadOp: "clear", storeOp: "store"
            }] });
            pass.setPipeline(fillPipeline);
            pass.setVertexBuffer(0, geometryBuffer);
            pass.setVertexBuffer(1, colorBuffer);
            pass.draw(4, model.controls.length);
            pass.setPipeline(textPipeline);
            pass.setBindGroup(0, bindings);
            pass.draw(3);
            pass.end();
            device.queue.submit([encoder.finish()]);
        }
    };
}
