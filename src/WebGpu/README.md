# StandardWasm.WebGpu

A generic WebGPU API for C#, in the spirit of Rust's `wgpu`. See [decision 0096](../../docs/decisions/0096-generic-webgpu-interface.md) and #313.

```csharp
var device = GpuDevice.FromHandle(backend, hostDeviceHandle);   // browser: device made by the host
var shader = device.CreateShaderModule(new GpuShaderModuleDescriptor { Wgsl = source });
var pipeline = device.CreateRenderPipeline(new GpuRenderPipelineDescriptor {
    Vertex = new GpuVertexState { Module = shader },
    Fragment = new GpuFragmentState { Module = shader, Targets = new[] { new GpuColorTargetState { Format = GpuTextureFormat.Bgra8Unorm } } },
});
var encoder = device.CreateCommandEncoder();
var pass = encoder.BeginRenderPass(new GpuRenderPassDescriptor { ColorAttachments = new[] { new GpuRenderPassColorAttachment { View = target } } });
pass.SetPipeline(pipeline);
pass.Draw(3);
pass.End();
device.Queue.Submit(encoder.Finish());
```

| Backend | Status |
| --- | --- |
| `RecordingBackend` | Complete. Deterministic call transcript, no GPU; validates release-once, liveness, write bounds and alignment |
| `Native.NativeBackend` (`webgpu.h`) | Handle and scalar entries bound; descriptor entries throw `NotSupportedException` naming the entry until #275 |
| `scripts/webgpu-imports.mjs` (browser) | Planned, milestone 3 |
