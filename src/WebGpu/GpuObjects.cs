using System;

namespace StandardWasm.WebGpu
{
    /// <summary>
    /// A managed owner of one webgpu.h handle. Invariant: Release is called exactly once, by
    /// Dispose; afterwards the handle is gone and every use throws ObjectDisposedException instead
    /// of passing a dangling handle to the backend.
    /// </summary>
    public abstract class GpuObject : IDisposable
    {
        private IntPtr _handle;

        protected GpuObject(IGpuBackend backend, GpuObjectKind kind, IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException(backend.Name + " returned a null " + kind + " handle.");
            Backend = backend;
            Kind = kind;
            _handle = handle;
        }

        public IGpuBackend Backend { get; }
        public GpuObjectKind Kind { get; }

        public IntPtr Handle
        {
            get
            {
                if (_handle == IntPtr.Zero) throw new ObjectDisposedException(Kind.ToString());
                return _handle;
            }
        }

        public bool IsDisposed => _handle == IntPtr.Zero;

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            Backend.Release(Kind, _handle);
            _handle = IntPtr.Zero;
        }
    }

    /// <summary>Entry point, like wgpu::Instance: picks a backend and requests an adapter.</summary>
    public sealed class GpuInstance
    {
        public GpuInstance(IGpuBackend backend) { Backend = backend; }
        public IGpuBackend Backend { get; }

        public GpuAdapter RequestAdapter(GpuPowerPreference power = GpuPowerPreference.HighPerformance)
            => new GpuAdapter(Backend, Backend.RequestAdapter(power));
    }

    public sealed class GpuAdapter : GpuObject
    {
        internal GpuAdapter(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.Adapter, h) { }
        public GpuDevice RequestDevice() => new GpuDevice(Backend, Backend.RequestDevice(Handle));
    }

    public sealed class GpuDevice : GpuObject
    {
        private GpuQueue _queue;

        internal GpuDevice(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.Device, h) { }

        /// <summary>Wraps a device the host already created (browser: before Main runs).</summary>
        public static GpuDevice FromHandle(IGpuBackend backend, IntPtr handle) => new GpuDevice(backend, handle);

        public GpuQueue Queue => _queue ?? (_queue = new GpuQueue(Backend, Backend.GetQueue(Handle)));

        public GpuBuffer CreateBuffer(GpuBufferDescriptor d) => new GpuBuffer(Backend, Backend.CreateBuffer(Handle, d), d.Size, d.Usage);
        public GpuTexture CreateTexture(GpuTextureDescriptor d) => new GpuTexture(Backend, Backend.CreateTexture(Handle, d), d);
        public GpuSampler CreateSampler(GpuSamplerDescriptor d) => new GpuSampler(Backend, Backend.CreateSampler(Handle, d));
        public GpuShaderModule CreateShaderModule(GpuShaderModuleDescriptor d) => new GpuShaderModule(Backend, Backend.CreateShaderModule(Handle, d));
        public GpuBindGroupLayout CreateBindGroupLayout(GpuBindGroupLayoutDescriptor d) => new GpuBindGroupLayout(Backend, Backend.CreateBindGroupLayout(Handle, d));
        public GpuBindGroup CreateBindGroup(GpuBindGroupDescriptor d) => new GpuBindGroup(Backend, Backend.CreateBindGroup(Handle, d));
        public GpuPipelineLayout CreatePipelineLayout(GpuPipelineLayoutDescriptor d) => new GpuPipelineLayout(Backend, Backend.CreatePipelineLayout(Handle, d));
        public GpuRenderPipeline CreateRenderPipeline(GpuRenderPipelineDescriptor d) => new GpuRenderPipeline(Backend, Backend.CreateRenderPipeline(Handle, d));
        public GpuComputePipeline CreateComputePipeline(GpuComputePipelineDescriptor d) => new GpuComputePipeline(Backend, Backend.CreateComputePipeline(Handle, d));
        public GpuCommandEncoder CreateCommandEncoder() => new GpuCommandEncoder(Backend, Backend.CreateCommandEncoder(Handle));
    }

    public sealed class GpuQueue : GpuObject
    {
        internal GpuQueue(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.Queue, h) { }

        public void WriteBuffer(GpuBuffer buffer, ulong bufferOffset, byte[] data)
            => Backend.WriteBuffer(Handle, buffer.Handle, bufferOffset, data, 0, data.Length);

        /// <summary>Little-endian float upload, the layout WGSL f32 and vecN&lt;f32&gt; expect.</summary>
        public void WriteBuffer(GpuBuffer buffer, ulong bufferOffset, float[] data)
            => WriteBuffer(buffer, bufferOffset, GpuBytes.FromFloats(data));

        public void WriteBuffer(GpuBuffer buffer, ulong bufferOffset, uint[] data)
            => WriteBuffer(buffer, bufferOffset, GpuBytes.FromUInts(data));

        public void Submit(params GpuCommandBuffer[] commandBuffers)
        {
            var handles = new IntPtr[commandBuffers.Length];
            for (int i = 0; i < handles.Length; i++) handles[i] = commandBuffers[i].Handle;
            Backend.Submit(Handle, handles);
        }
    }

    public sealed class GpuBuffer : GpuObject
    {
        internal GpuBuffer(IGpuBackend b, IntPtr h, ulong size, GpuBufferUsage usage) : base(b, GpuObjectKind.Buffer, h)
        {
            Size = size;
            Usage = usage;
        }

        public ulong Size { get; }
        public GpuBufferUsage Usage { get; }
        public void Destroy() => Backend.Destroy(GpuObjectKind.Buffer, Handle);
    }

    public sealed class GpuTexture : GpuObject
    {
        internal GpuTexture(IGpuBackend b, IntPtr h, GpuTextureDescriptor d) : base(b, GpuObjectKind.Texture, h)
        {
            Width = d.Width;
            Height = d.Height;
            Layers = d.DepthOrArrayLayers;
            MipLevels = d.MipLevelCount;
            Format = d.Format;
        }

        public uint Width { get; }
        public uint Height { get; }
        public uint Layers { get; }
        public uint MipLevels { get; }
        public GpuTextureFormat Format { get; }
        public GpuTextureView CreateView(GpuTextureViewDescriptor descriptor = null) => new GpuTextureView(Backend, Backend.CreateTextureView(Handle, descriptor));

        /// <summary>A view of one mip level of one array layer: a render target for baking.</summary>
        public GpuTextureView CreateLayerView(uint layer, uint mip = 0) => CreateView(new GpuTextureViewDescriptor
        {
            Dimension = GpuTextureViewDimension.D2,
            BaseArrayLayer = layer,
            ArrayLayerCount = 1,
            BaseMipLevel = mip,
            MipLevelCount = 1,
        });
        public void Destroy() => Backend.Destroy(GpuObjectKind.Texture, Handle);
    }

    public sealed class GpuTextureView : GpuObject
    {
        internal GpuTextureView(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.TextureView, h) { }

        /// <summary>Wraps a view the host owns, such as the current surface (canvas) texture.</summary>
        public static GpuTextureView FromHandle(IGpuBackend backend, IntPtr handle) => new GpuTextureView(backend, handle);
    }

    public sealed class GpuSampler : GpuObject
    {
        internal GpuSampler(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.Sampler, h) { }
    }

    public sealed class GpuShaderModule : GpuObject
    {
        internal GpuShaderModule(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.ShaderModule, h) { }
    }

    public sealed class GpuBindGroupLayout : GpuObject
    {
        internal GpuBindGroupLayout(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.BindGroupLayout, h) { }
    }

    public sealed class GpuBindGroup : GpuObject
    {
        internal GpuBindGroup(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.BindGroup, h) { }
    }

    public sealed class GpuPipelineLayout : GpuObject
    {
        internal GpuPipelineLayout(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.PipelineLayout, h) { }
    }

    public sealed class GpuRenderPipeline : GpuObject
    {
        internal GpuRenderPipeline(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.RenderPipeline, h) { }
        public GpuBindGroupLayout GetBindGroupLayout(uint group) => new GpuBindGroupLayout(Backend, Backend.GetBindGroupLayout(Kind, Handle, group));
    }

    public sealed class GpuComputePipeline : GpuObject
    {
        internal GpuComputePipeline(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.ComputePipeline, h) { }
        public GpuBindGroupLayout GetBindGroupLayout(uint group) => new GpuBindGroupLayout(Backend, Backend.GetBindGroupLayout(Kind, Handle, group));
    }

    public sealed class GpuCommandEncoder : GpuObject
    {
        internal GpuCommandEncoder(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.CommandEncoder, h) { }

        public GpuRenderPassEncoder BeginRenderPass(GpuRenderPassDescriptor d) => new GpuRenderPassEncoder(Backend, Backend.BeginRenderPass(Handle, d));
        public GpuComputePassEncoder BeginComputePass() => new GpuComputePassEncoder(Backend, Backend.BeginComputePass(Handle));

        /// <summary>Finishing consumes the encoder, as in WebGPU; the encoder is released here.</summary>
        public GpuCommandBuffer Finish()
        {
            var commands = new GpuCommandBuffer(Backend, Backend.Finish(Handle));
            Dispose();
            return commands;
        }
    }

    public sealed class GpuCommandBuffer : GpuObject
    {
        internal GpuCommandBuffer(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.CommandBuffer, h) { }
    }

    public sealed class GpuRenderPassEncoder : GpuObject
    {
        internal GpuRenderPassEncoder(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.RenderPassEncoder, h) { }

        public void SetPipeline(GpuRenderPipeline p) => Backend.SetRenderPipeline(Handle, p.Handle);
        public void SetBindGroup(uint index, GpuBindGroup g) => Backend.SetRenderBindGroup(Handle, index, g.Handle);
        public void SetVertexBuffer(uint slot, GpuBuffer b, ulong offset = 0, ulong size = 0) => Backend.SetVertexBuffer(Handle, slot, b.Handle, offset, size == 0 ? b.Size - offset : size);
        public void SetIndexBuffer(GpuBuffer b, GpuIndexFormat format, ulong offset = 0, ulong size = 0) => Backend.SetIndexBuffer(Handle, b.Handle, format, offset, size == 0 ? b.Size - offset : size);
        public void SetViewport(float x, float y, float w, float h, float minDepth = 0f, float maxDepth = 1f) => Backend.SetViewport(Handle, x, y, w, h, minDepth, maxDepth);
        public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0) => Backend.Draw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);
        public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0) => Backend.DrawIndexed(Handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
        public void DrawIndirect(GpuBuffer indirect, ulong offset = 0) => Backend.DrawIndirect(Handle, indirect.Handle, offset);

        /// <summary>Ending a pass consumes the encoder; it is released here.</summary>
        public void End()
        {
            Backend.EndRenderPass(Handle);
            Dispose();
        }
    }

    public sealed class GpuComputePassEncoder : GpuObject
    {
        internal GpuComputePassEncoder(IGpuBackend b, IntPtr h) : base(b, GpuObjectKind.ComputePassEncoder, h) { }

        public void SetPipeline(GpuComputePipeline p) => Backend.SetComputePipeline(Handle, p.Handle);
        public void SetBindGroup(uint index, GpuBindGroup g) => Backend.SetComputeBindGroup(Handle, index, g.Handle);
        public void DispatchWorkgroups(uint x, uint y = 1, uint z = 1) => Backend.DispatchWorkgroups(Handle, x, y, z);

        public void End()
        {
            Backend.EndComputePass(Handle);
            Dispose();
        }
    }

    /// <summary>Little-endian packing without BitConverter's host-endian dependence.</summary>
    public static class GpuBytes
    {
        public static byte[] FromFloats(float[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                uint bits = SingleToUInt32Bits(values[i]);
                bytes[i * 4] = (byte)bits;
                bytes[i * 4 + 1] = (byte)(bits >> 8);
                bytes[i * 4 + 2] = (byte)(bits >> 16);
                bytes[i * 4 + 3] = (byte)(bits >> 24);
            }
            return bytes;
        }

        public static byte[] FromUInts(uint[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                uint v = values[i];
                bytes[i * 4] = (byte)v;
                bytes[i * 4 + 1] = (byte)(v >> 8);
                bytes[i * 4 + 2] = (byte)(v >> 16);
                bytes[i * 4 + 3] = (byte)(v >> 24);
            }
            return bytes;
        }

        // netstandard2.0 has no BitConverter.SingleToInt32Bits. GetBytes and ToInt32 both use host
        // order, so the round trip reinterprets the float's bits exactly on any host; the
        // little-endian packing above then fixes the byte order.
        private static uint SingleToUInt32Bits(float value)
            => unchecked((uint)BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
    }
}
