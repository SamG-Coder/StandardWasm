using System;
using System.Collections.Generic;
using System.Text;

namespace StandardWasm.WebGpu
{
    /// <summary>
    /// A backend with no GPU: it hands out sequential fake handles and records every call as one
    /// line of text. A renderer's frame becomes a deterministic transcript that can be compared
    /// between the CLR and StandardWasm, as every StandardWasm fixture compares results.
    /// It validates what WebGPU validates cheaply (release once, handle liveness, write bounds and
    /// 4-byte alignment) and throws InvalidOperationException naming the entry, never a silent pass.
    /// </summary>
    public sealed class RecordingBackend : IGpuBackend
    {
        private readonly List<string> _calls = new List<string>();
        private readonly Dictionary<long, GpuObjectKind> _live = new Dictionary<long, GpuObjectKind>();
        private readonly Dictionary<long, ulong> _bufferSizes = new Dictionary<long, ulong>();
        private long _next = 1;

        public string Name => "recording";
        public IReadOnlyList<string> Calls => _calls;
        public int LiveObjectCount => _live.Count;
        public ulong BytesWritten { get; private set; }
        public ulong VerticesDrawn { get; private set; }

        public string Transcript()
        {
            var sb = new StringBuilder();
            foreach (var c in _calls) sb.Append(c).Append('\n');
            return sb.ToString();
        }

        private IntPtr New(GpuObjectKind kind, string call)
        {
            long h = _next++;
            _live[h] = kind;
            _calls.Add("#" + h + " = " + call);
            return new IntPtr(h);
        }

        private string H(IntPtr handle)
        {
            long h = handle.ToInt64();
            if (!_live.ContainsKey(h)) throw new InvalidOperationException("Use of a released or unknown handle #" + h + ".");
            return "#" + h;
        }

        private string H(GpuObject o) => o == null ? "null" : H(o.Handle);

        public IntPtr RequestAdapter(GpuPowerPreference power) => New(GpuObjectKind.Adapter, "requestAdapter(" + power + ")");
        public IntPtr RequestDevice(IntPtr adapter) => New(GpuObjectKind.Device, "requestDevice(" + H(adapter) + ")");
        public IntPtr GetQueue(IntPtr device) => New(GpuObjectKind.Queue, "getQueue(" + H(device) + ")");

        public IntPtr CreateBuffer(IntPtr device, GpuBufferDescriptor d)
        {
            if (d.Size % 4 != 0) throw new InvalidOperationException("wgpuDeviceCreateBuffer: size " + d.Size + " is not a multiple of 4.");
            var h = New(GpuObjectKind.Buffer, "createBuffer(" + H(device) + ", '" + d.Label + "', " + d.Size + ", " + d.Usage + ")");
            _bufferSizes[h.ToInt64()] = d.Size;
            return h;
        }

        public IntPtr CreateTexture(IntPtr device, GpuTextureDescriptor d) =>
            New(GpuObjectKind.Texture, "createTexture(" + H(device) + ", '" + d.Label + "', " + d.Width + "x" + d.Height + ", " + d.Format + ", " + d.Usage + ")");

        public IntPtr CreateTextureView(IntPtr texture, GpuTextureViewDescriptor d) => New(GpuObjectKind.TextureView, "createView(" + H(texture) +
            (d == null ? "" : ", " + (d.Dimension?.ToString() ?? "default") + " mip " + d.BaseMipLevel + "+" + d.MipLevelCount + " layer " + d.BaseArrayLayer + "+" + d.ArrayLayerCount) + ")");
        public IntPtr CreateSampler(IntPtr device, GpuSamplerDescriptor d) => New(GpuObjectKind.Sampler, "createSampler(" + H(device) + ", " + d.MinFilter + ")");

        public IntPtr CreateShaderModule(IntPtr device, GpuShaderModuleDescriptor d)
        {
            if (string.IsNullOrEmpty(d.Wgsl)) throw new InvalidOperationException("wgpuDeviceCreateShaderModule: empty WGSL.");
            return New(GpuObjectKind.ShaderModule, "createShaderModule(" + H(device) + ", '" + d.Label + "', " + d.Wgsl.Length + " chars)");
        }

        public IntPtr CreateBindGroupLayout(IntPtr device, GpuBindGroupLayoutDescriptor d) =>
            New(GpuObjectKind.BindGroupLayout, "createBindGroupLayout(" + H(device) + ", '" + d.Label + "', " + d.Entries.Length + " entries)");

        public IntPtr CreateBindGroup(IntPtr device, GpuBindGroupDescriptor d)
        {
            var sb = new StringBuilder();
            foreach (var e in d.Entries)
            {
                sb.Append(' ').Append(e.Binding).Append(':');
                sb.Append(e.Buffer != null ? H(e.Buffer) : e.Sampler != null ? H(e.Sampler) : H(e.TextureView));
            }
            return New(GpuObjectKind.BindGroup, "createBindGroup(" + H(device) + ", '" + d.Label + "', layout " + H(d.Layout) + "," + sb + ")");
        }

        public IntPtr CreatePipelineLayout(IntPtr device, GpuPipelineLayoutDescriptor d) =>
            New(GpuObjectKind.PipelineLayout, "createPipelineLayout(" + H(device) + ", " + d.BindGroupLayouts.Length + " groups)");

        public IntPtr CreateRenderPipeline(IntPtr device, GpuRenderPipelineDescriptor d) =>
            New(GpuObjectKind.RenderPipeline, "createRenderPipeline(" + H(device) + ", '" + d.Label + "', vs " + H(d.Vertex.Module) + ":" + d.Vertex.EntryPoint +
                (d.Fragment != null ? ", fs " + H(d.Fragment.Module) + ":" + d.Fragment.EntryPoint : "") + ", " + d.Primitive.Topology + ")");

        public IntPtr CreateComputePipeline(IntPtr device, GpuComputePipelineDescriptor d) =>
            New(GpuObjectKind.ComputePipeline, "createComputePipeline(" + H(device) + ", '" + d.Label + "', " + H(d.Module) + ":" + d.EntryPoint + ")");

        public IntPtr GetBindGroupLayout(GpuObjectKind kind, IntPtr pipeline, uint groupIndex) =>
            New(GpuObjectKind.BindGroupLayout, "getBindGroupLayout(" + H(pipeline) + ", " + groupIndex + ")");

        public void WriteBuffer(IntPtr queue, IntPtr buffer, ulong bufferOffset, byte[] data, int dataOffset, int size)
        {
            ulong cap = _bufferSizes[buffer.ToInt64()];
            if (bufferOffset % 4 != 0 || size % 4 != 0)
                throw new InvalidOperationException("wgpuQueueWriteBuffer: offset and size must be multiples of 4.");
            if (bufferOffset + (ulong)size > cap)
                throw new InvalidOperationException("wgpuQueueWriteBuffer: " + size + " bytes at " + bufferOffset + " overrun a " + cap + "-byte buffer.");
            BytesWritten += (ulong)size;
            _calls.Add("writeBuffer(" + H(queue) + ", " + H(buffer) + ", @" + bufferOffset + ", " + size + " bytes, fnv " + Fnv(data, dataOffset, size).ToString("x8") + ")");
        }

        public void Submit(IntPtr queue, IntPtr[] commandBuffers)
        {
            var sb = new StringBuilder();
            foreach (var c in commandBuffers) sb.Append(' ').Append(H(c));
            _calls.Add("submit(" + H(queue) + "," + sb + ")");
        }

        public IntPtr CreateCommandEncoder(IntPtr device) => New(GpuObjectKind.CommandEncoder, "createCommandEncoder(" + H(device) + ")");

        public IntPtr BeginRenderPass(IntPtr encoder, GpuRenderPassDescriptor d)
        {
            var c = d.ColorAttachments[0];
            return New(GpuObjectKind.RenderPassEncoder, "beginRenderPass(" + H(encoder) + ", color " + H(c.View) + " " + c.LoadOp +
                (d.DepthStencilAttachment != null ? ", depth " + H(d.DepthStencilAttachment.View) : "") + ")");
        }

        public IntPtr BeginComputePass(IntPtr encoder) => New(GpuObjectKind.ComputePassEncoder, "beginComputePass(" + H(encoder) + ")");
        public IntPtr Finish(IntPtr encoder) => New(GpuObjectKind.CommandBuffer, "finish(" + H(encoder) + ")");

        public void SetRenderPipeline(IntPtr pass, IntPtr pipeline) => _calls.Add("setPipeline(" + H(pass) + ", " + H(pipeline) + ")");
        public void SetRenderBindGroup(IntPtr pass, uint i, IntPtr g) => _calls.Add("setBindGroup(" + H(pass) + ", " + i + ", " + H(g) + ")");
        public void SetVertexBuffer(IntPtr pass, uint slot, IntPtr b, ulong o, ulong s) => _calls.Add("setVertexBuffer(" + H(pass) + ", " + slot + ", " + H(b) + ")");
        public void SetIndexBuffer(IntPtr pass, IntPtr b, GpuIndexFormat f, ulong o, ulong s) => _calls.Add("setIndexBuffer(" + H(pass) + ", " + H(b) + ", " + f + ")");
        public void SetViewport(IntPtr pass, float x, float y, float w, float h, float mn, float mx) => _calls.Add("setViewport(" + H(pass) + ", " + w + "x" + h + ")");

        public void Draw(IntPtr pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        {
            VerticesDrawn += (ulong)vertexCount * instanceCount;
            _calls.Add("draw(" + H(pass) + ", " + vertexCount + " x " + instanceCount + ", first " + firstVertex + "/" + firstInstance + ")");
        }

        public void DrawIndexed(IntPtr pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
        {
            VerticesDrawn += (ulong)indexCount * instanceCount;
            _calls.Add("drawIndexed(" + H(pass) + ", " + indexCount + " x " + instanceCount + ")");
        }

        public void DrawIndirect(IntPtr pass, IntPtr buffer, ulong offset) => _calls.Add("drawIndirect(" + H(pass) + ", " + H(buffer) + ", @" + offset + ")");
        public void EndRenderPass(IntPtr pass) => _calls.Add("end(" + H(pass) + ")");

        public void SetComputePipeline(IntPtr pass, IntPtr pipeline) => _calls.Add("setPipeline(" + H(pass) + ", " + H(pipeline) + ")");
        public void SetComputeBindGroup(IntPtr pass, uint i, IntPtr g) => _calls.Add("setBindGroup(" + H(pass) + ", " + i + ", " + H(g) + ")");
        public void DispatchWorkgroups(IntPtr pass, uint x, uint y, uint z) => _calls.Add("dispatch(" + H(pass) + ", " + x + ", " + y + ", " + z + ")");
        public void EndComputePass(IntPtr pass) => _calls.Add("end(" + H(pass) + ")");

        public void Destroy(GpuObjectKind kind, IntPtr handle) => _calls.Add("destroy(" + H(handle) + ")");

        public void Release(GpuObjectKind kind, IntPtr handle)
        {
            long h = handle.ToInt64();
            GpuObjectKind recorded;
            if (!_live.TryGetValue(h, out recorded)) throw new InvalidOperationException("wgpu" + kind + "Release: #" + h + " released twice or never created.");
            if (recorded != kind) throw new InvalidOperationException("wgpu" + kind + "Release on #" + h + ", which is a " + recorded + ".");
            _live.Remove(h);
            _bufferSizes.Remove(h);
            _calls.Add("release(#" + h + ")");
        }

        private static uint Fnv(byte[] data, int offset, int size)
        {
            uint h = 2166136261;
            for (int i = 0; i < size; i++) { h ^= data[offset + i]; h *= 16777619; }
            return h;
        }
    }
}
