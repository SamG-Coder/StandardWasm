using System;

namespace StandardWasm.WebGpu.Native
{
    /// <summary>
    /// IGpuBackend over webgpu.h (decision 0096). Milestone 1 implements every entry that lowers
    /// today: handles and scalars. Entries that need descriptor structs, strings or callbacks
    /// throw NotSupportedException naming the webgpu.h entry and the dependency. They are never
    /// silently skipped. The adapter and device come from the host (GpuDevice.FromHandle) until
    /// the callback entries land.
    /// </summary>
    public sealed class NativeBackend : IGpuBackend
    {
        public string Name => "webgpu.h";

        private static NotSupportedException Waits(string entry, string dependency) =>
            new NotSupportedException(entry + " is not bound yet: it needs " + dependency + " (decision 0096).");

        private const string Structs = "descriptor struct and WGPUStringView marshalling (#275)";
        private const string Callbacks = "a callback-info struct and function pointers (decision 0078) or a host shim";

        public IntPtr RequestAdapter(GpuPowerPreference power) => throw Waits("wgpuInstanceRequestAdapter", Callbacks);
        public IntPtr RequestDevice(IntPtr adapter) => throw Waits("wgpuAdapterRequestDevice", Callbacks);
        public IntPtr GetQueue(IntPtr device) => WebGpuNative.wgpuDeviceGetQueue(device);

        public IntPtr CreateBuffer(IntPtr d, GpuBufferDescriptor x) => throw Waits("wgpuDeviceCreateBuffer", Structs);
        public IntPtr CreateTexture(IntPtr d, GpuTextureDescriptor x) => throw Waits("wgpuDeviceCreateTexture", Structs);
        public IntPtr CreateTextureView(IntPtr texture) => WebGpuNative.wgpuTextureCreateView(texture, IntPtr.Zero);
        public IntPtr CreateSampler(IntPtr d, GpuSamplerDescriptor x) => throw Waits("wgpuDeviceCreateSampler", Structs);
        public IntPtr CreateShaderModule(IntPtr d, GpuShaderModuleDescriptor x) => throw Waits("wgpuDeviceCreateShaderModule", Structs);
        public IntPtr CreateBindGroupLayout(IntPtr d, GpuBindGroupLayoutDescriptor x) => throw Waits("wgpuDeviceCreateBindGroupLayout", Structs);
        public IntPtr CreateBindGroup(IntPtr d, GpuBindGroupDescriptor x) => throw Waits("wgpuDeviceCreateBindGroup", Structs);
        public IntPtr CreatePipelineLayout(IntPtr d, GpuPipelineLayoutDescriptor x) => throw Waits("wgpuDeviceCreatePipelineLayout", Structs);
        public IntPtr CreateRenderPipeline(IntPtr d, GpuRenderPipelineDescriptor x) => throw Waits("wgpuDeviceCreateRenderPipeline", Structs);
        public IntPtr CreateComputePipeline(IntPtr d, GpuComputePipelineDescriptor x) => throw Waits("wgpuDeviceCreateComputePipeline", Structs);

        public IntPtr GetBindGroupLayout(GpuObjectKind kind, IntPtr pipeline, uint group) =>
            kind == GpuObjectKind.ComputePipeline
                ? WebGpuNative.wgpuComputePipelineGetBindGroupLayout(pipeline, group)
                : WebGpuNative.wgpuRenderPipelineGetBindGroupLayout(pipeline, group);

        public void WriteBuffer(IntPtr q, IntPtr b, ulong o, byte[] data, int off, int size) => throw Waits("wgpuQueueWriteBuffer", "pinned managed arrays as data pointers (#275)");
        public void Submit(IntPtr q, IntPtr[] commandBuffers) => throw Waits("wgpuQueueSubmit", "a pinned handle array (#275)");
        public IntPtr CreateCommandEncoder(IntPtr device) => throw Waits("wgpuDeviceCreateCommandEncoder", Structs);
        public IntPtr BeginRenderPass(IntPtr e, GpuRenderPassDescriptor x) => throw Waits("wgpuCommandEncoderBeginRenderPass", Structs);
        public IntPtr BeginComputePass(IntPtr e) => throw Waits("wgpuCommandEncoderBeginComputePass", Structs);
        public IntPtr Finish(IntPtr e) => throw Waits("wgpuCommandEncoderFinish", Structs);

        public void SetRenderPipeline(IntPtr p, IntPtr pipeline) => WebGpuNative.wgpuRenderPassEncoderSetPipeline(p, pipeline);
        public void SetRenderBindGroup(IntPtr p, uint i, IntPtr g) => WebGpuNative.wgpuRenderPassEncoderSetBindGroup(p, i, g, UIntPtr.Zero, IntPtr.Zero);
        public void SetVertexBuffer(IntPtr p, uint slot, IntPtr b, ulong o, ulong s) => WebGpuNative.wgpuRenderPassEncoderSetVertexBuffer(p, slot, b, o, s);
        public void SetIndexBuffer(IntPtr p, IntPtr b, GpuIndexFormat f, ulong o, ulong s) => throw Waits("wgpuRenderPassEncoderSetIndexBuffer", "the pinned header's WGPUIndexFormat values (milestone 2)");
        public void SetViewport(IntPtr p, float x, float y, float w, float h, float mn, float mx) => WebGpuNative.wgpuRenderPassEncoderSetViewport(p, x, y, w, h, mn, mx);
        public void Draw(IntPtr p, uint v, uint i, uint fv, uint fi) => WebGpuNative.wgpuRenderPassEncoderDraw(p, v, i, fv, fi);
        public void DrawIndexed(IntPtr p, uint n, uint i, uint fi, int bv, uint fin) => WebGpuNative.wgpuRenderPassEncoderDrawIndexed(p, n, i, fi, bv, fin);
        public void DrawIndirect(IntPtr p, IntPtr b, ulong o) => WebGpuNative.wgpuRenderPassEncoderDrawIndirect(p, b, o);
        public void EndRenderPass(IntPtr p) => WebGpuNative.wgpuRenderPassEncoderEnd(p);

        public void SetComputePipeline(IntPtr p, IntPtr pipeline) => WebGpuNative.wgpuComputePassEncoderSetPipeline(p, pipeline);
        public void SetComputeBindGroup(IntPtr p, uint i, IntPtr g) => WebGpuNative.wgpuComputePassEncoderSetBindGroup(p, i, g, UIntPtr.Zero, IntPtr.Zero);
        public void DispatchWorkgroups(IntPtr p, uint x, uint y, uint z) => WebGpuNative.wgpuComputePassEncoderDispatchWorkgroups(p, x, y, z);
        public void EndComputePass(IntPtr p) => WebGpuNative.wgpuComputePassEncoderEnd(p);

        public void Destroy(GpuObjectKind kind, IntPtr h)
        {
            if (kind == GpuObjectKind.Buffer) WebGpuNative.wgpuBufferDestroy(h);
            else if (kind == GpuObjectKind.Texture) WebGpuNative.wgpuTextureDestroy(h);
            else throw new ArgumentException("Only buffers and textures have a Destroy entry.", nameof(kind));
        }

        public void Release(GpuObjectKind kind, IntPtr h)
        {
            switch (kind)
            {
                case GpuObjectKind.Adapter: WebGpuNative.wgpuAdapterRelease(h); break;
                case GpuObjectKind.Device: WebGpuNative.wgpuDeviceRelease(h); break;
                case GpuObjectKind.Queue: WebGpuNative.wgpuQueueRelease(h); break;
                case GpuObjectKind.Buffer: WebGpuNative.wgpuBufferRelease(h); break;
                case GpuObjectKind.Texture: WebGpuNative.wgpuTextureRelease(h); break;
                case GpuObjectKind.TextureView: WebGpuNative.wgpuTextureViewRelease(h); break;
                case GpuObjectKind.Sampler: WebGpuNative.wgpuSamplerRelease(h); break;
                case GpuObjectKind.ShaderModule: WebGpuNative.wgpuShaderModuleRelease(h); break;
                case GpuObjectKind.BindGroupLayout: WebGpuNative.wgpuBindGroupLayoutRelease(h); break;
                case GpuObjectKind.BindGroup: WebGpuNative.wgpuBindGroupRelease(h); break;
                case GpuObjectKind.PipelineLayout: WebGpuNative.wgpuPipelineLayoutRelease(h); break;
                case GpuObjectKind.RenderPipeline: WebGpuNative.wgpuRenderPipelineRelease(h); break;
                case GpuObjectKind.ComputePipeline: WebGpuNative.wgpuComputePipelineRelease(h); break;
                case GpuObjectKind.CommandEncoder: WebGpuNative.wgpuCommandEncoderRelease(h); break;
                case GpuObjectKind.CommandBuffer: WebGpuNative.wgpuCommandBufferRelease(h); break;
                case GpuObjectKind.RenderPassEncoder: WebGpuNative.wgpuRenderPassEncoderRelease(h); break;
                case GpuObjectKind.ComputePassEncoder: WebGpuNative.wgpuComputePassEncoderRelease(h); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
