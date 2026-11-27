using System;

namespace StandardWasm.WebGpu
{
    /// <summary>
    /// The seam between the managed API and an implementation of webgpu.h (decision 0096), the
    /// counterpart of wgpu's core/webgpu split. Every object crosses as an opaque handle: a
    /// pointer-sized value, which StandardWasm lowers as wasm32 i32 (decision 0083, native int).
    /// IntPtr.Zero is never a valid handle.
    /// </summary>
    public interface IGpuBackend
    {
        string Name { get; }

        // Adapter and device acquisition is asynchronous in webgpu.h (callback-info structs). A
        // backend resolves it before returning; in a browser the host obtains the device before
        // Main runs and hands its handle in.
        IntPtr RequestAdapter(GpuPowerPreference power);
        IntPtr RequestDevice(IntPtr adapter);
        IntPtr GetQueue(IntPtr device);

        IntPtr CreateBuffer(IntPtr device, GpuBufferDescriptor descriptor);
        IntPtr CreateTexture(IntPtr device, GpuTextureDescriptor descriptor);
        /// <summary>descriptor null: a view of the whole texture.</summary>
        IntPtr CreateTextureView(IntPtr texture, GpuTextureViewDescriptor descriptor);
        IntPtr CreateSampler(IntPtr device, GpuSamplerDescriptor descriptor);
        IntPtr CreateShaderModule(IntPtr device, GpuShaderModuleDescriptor descriptor);
        IntPtr CreateBindGroupLayout(IntPtr device, GpuBindGroupLayoutDescriptor descriptor);
        IntPtr CreateBindGroup(IntPtr device, GpuBindGroupDescriptor descriptor);
        IntPtr CreatePipelineLayout(IntPtr device, GpuPipelineLayoutDescriptor descriptor);
        IntPtr CreateRenderPipeline(IntPtr device, GpuRenderPipelineDescriptor descriptor);
        IntPtr CreateComputePipeline(IntPtr device, GpuComputePipelineDescriptor descriptor);
        IntPtr GetBindGroupLayout(GpuObjectKind pipelineKind, IntPtr pipeline, uint groupIndex);

        void WriteBuffer(IntPtr queue, IntPtr buffer, ulong bufferOffset, byte[] data, int dataOffset, int size);
        void Submit(IntPtr queue, IntPtr[] commandBuffers);

        IntPtr CreateCommandEncoder(IntPtr device);
        IntPtr BeginRenderPass(IntPtr encoder, GpuRenderPassDescriptor descriptor);
        IntPtr BeginComputePass(IntPtr encoder);
        IntPtr Finish(IntPtr encoder);

        void SetRenderPipeline(IntPtr pass, IntPtr pipeline);
        void SetRenderBindGroup(IntPtr pass, uint groupIndex, IntPtr bindGroup);
        void SetVertexBuffer(IntPtr pass, uint slot, IntPtr buffer, ulong offset, ulong size);
        void SetIndexBuffer(IntPtr pass, IntPtr buffer, GpuIndexFormat format, ulong offset, ulong size);
        void SetViewport(IntPtr pass, float x, float y, float width, float height, float minDepth, float maxDepth);
        void Draw(IntPtr pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
        void DrawIndexed(IntPtr pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance);
        void DrawIndirect(IntPtr pass, IntPtr indirectBuffer, ulong indirectOffset);
        void EndRenderPass(IntPtr pass);

        void SetComputePipeline(IntPtr pass, IntPtr pipeline);
        void SetComputeBindGroup(IntPtr pass, uint groupIndex, IntPtr bindGroup);
        void DispatchWorkgroups(IntPtr pass, uint x, uint y, uint z);
        void EndComputePass(IntPtr pass);

        /// <summary>Destroys GPU memory now (buffers and textures); Release drops the reference.</summary>
        void Destroy(GpuObjectKind kind, IntPtr handle);
        void Release(GpuObjectKind kind, IntPtr handle);
    }

    /// <summary>Which wgpu*Release entry a handle needs.</summary>
    public enum GpuObjectKind
    {
        Adapter,
        Device,
        Queue,
        Buffer,
        Texture,
        TextureView,
        Sampler,
        ShaderModule,
        BindGroupLayout,
        BindGroup,
        PipelineLayout,
        RenderPipeline,
        ComputePipeline,
        CommandEncoder,
        CommandBuffer,
        RenderPassEncoder,
        ComputePassEncoder,
    }
}
