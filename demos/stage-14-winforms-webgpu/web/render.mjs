const tones = {
    background: [0.078, 0.113, 0.180],
    panel: [0.117, 0.166, 0.255],
    button: [0.384, 0.855, 0.906]
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
                paint.font = button ? "700 16px system-ui" : "700 34px system-ui";
                paint.fillStyle = button ? "#10212b" : "#f1f6ff";
                paint.textAlign = button ? "center" : "left";
                paint.textBaseline = "middle";
                paint.fillText(texts.get(control.name),
                    control.x + (button ? control.width * 0.5 : 20),
                    control.y + control.height * 0.5,
                    control.width - (button ? 12 : 40));
            }
        }
    };
}

export function canvasBaseline(model, text) {
    const canvas = new OffscreenCanvas(model.width, model.height);
    const context = canvas.getContext("2d", { alpha: false });
    return () => {
        context.fillStyle = rgb(tones.background);
        context.fillRect(0, 0, model.width, model.height);
        for (const c of model.controls) {
            context.fillStyle = rgb(c.kind === "button" ? tones.button : tones.panel);
            context.fillRect(c.x, c.y, c.width, c.height);
        }
        context.drawImage(text.canvas, 0, 0);
    };
}

const fills = /* wgsl */ `
struct Vertex {
    @builtin(position) position: vec4f,
    @location(0) color: vec3f,
};
@vertex fn vertexMain(@location(0) pos: vec2f, @location(1) color: vec3f) -> Vertex {
    return Vertex(vec4f(pos, 0, 1), color);
}
@fragment fn fragmentMain(in: Vertex) -> @location(0) vec4f {
    return vec4f(in.color, 1);
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
    const adapter = await navigator.gpu.requestAdapter({ powerPreference: "high-performance" });
    if (!adapter) throw Error("No WebGPU adapter is available.");
    const device = await adapter.requestDevice();
    const context = canvas.getContext("webgpu");
    if (!context) throw Error("The WebGPU canvas is unavailable.");
    const format = navigator.gpu.getPreferredCanvasFormat();
    context.configure({ device, format, alphaMode: "opaque" });

    const vertices = [];
    for (const c of model.controls) {
        const left = 2 * c.x / model.width - 1;
        const right = 2 * (c.x + c.width) / model.width - 1;
        const top = 1 - 2 * c.y / model.height;
        const bottom = 1 - 2 * (c.y + c.height) / model.height;
        const color = c.kind === "button" ? tones.button : tones.panel;
        for (const [x, y] of [[left, top], [left, bottom], [right, top], [right, top], [left, bottom], [right, bottom]])
            vertices.push(x, y, ...color);
    }
    const data = new Float32Array(vertices);
    const vertexBuffer = device.createBuffer({
        size: data.byteLength,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST
    });
    device.queue.writeBuffer(vertexBuffer, 0, data);
    const fillShader = device.createShaderModule({ code: fills });
    const glyphShader = device.createShaderModule({ code: glyphs });
    const fillPipeline = await device.createRenderPipelineAsync({
        layout: "auto",
        vertex: {
            module: fillShader, entryPoint: "vertexMain",
            buffers: [{ arrayStride: 20, attributes: [
                { shaderLocation: 0, offset: 0, format: "float32x2" },
                { shaderLocation: 1, offset: 8, format: "float32x3" }
            ] }]
        },
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
            pass.setVertexBuffer(0, vertexBuffer);
            pass.draw(vertices.length / 5);
            pass.setPipeline(textPipeline);
            pass.setBindGroup(0, bindings);
            pass.draw(3);
            pass.end();
            device.queue.submit([encoder.finish()]);
        }
    };
}
