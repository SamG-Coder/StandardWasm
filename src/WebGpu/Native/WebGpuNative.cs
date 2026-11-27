using System;
using System.Runtime.InteropServices;

namespace StandardWasm.WebGpu.Native
{
    /// <summary>
    /// webgpu.h entry points bound by name (decision 0096). On the CLR the library "webgpu" is
    /// wgpu-native or Dawn; under StandardWasm each entry becomes the import native:webgpu/&lt;entry&gt;
    /// (decision 0083), answered by scripts/webgpu-imports.mjs.
    ///
    /// Only entries whose parameters are handles (IntPtr, lowered as native int) or blittable
    /// scalars are declared here: those lower today. Descriptor-taking entries need struct and
    /// string marshalling (#275) and are added with their header-layout structs in milestone 2.
    /// Enum parameters (WGPUIndexFormat) cross as uint; the backend maps managed enums to the
    /// pinned header's values explicitly and never casts them.
    /// </summary>
    public static class WebGpuNative
    {
        public const string Library = "webgpu";

        // ---- device and queue ----
        [DllImport(Library)] public static extern IntPtr wgpuDeviceGetQueue(IntPtr device);

        // ---- buffers and textures ----
        [DllImport(Library)] public static extern ulong wgpuBufferGetSize(IntPtr buffer);
        [DllImport(Library)] public static extern void wgpuBufferDestroy(IntPtr buffer);
        [DllImport(Library)] public static extern void wgpuTextureDestroy(IntPtr texture);
        [DllImport(Library)] public static extern uint wgpuTextureGetWidth(IntPtr texture);
        [DllImport(Library)] public static extern uint wgpuTextureGetHeight(IntPtr texture);
        /// <summary>descriptor may be IntPtr.Zero: a view of the whole texture.</summary>
        [DllImport(Library)] public static extern IntPtr wgpuTextureCreateView(IntPtr texture, IntPtr descriptor);

        // ---- pipelines ----
        [DllImport(Library)] public static extern IntPtr wgpuRenderPipelineGetBindGroupLayout(IntPtr pipeline, uint groupIndex);
        [DllImport(Library)] public static extern IntPtr wgpuComputePipelineGetBindGroupLayout(IntPtr pipeline, uint groupIndex);

        // ---- render pass ----
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetPipeline(IntPtr pass, IntPtr pipeline);
        /// <summary>dynamicOffsets may be IntPtr.Zero when dynamicOffsetCount is 0.</summary>
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetBindGroup(IntPtr pass, uint groupIndex, IntPtr group, UIntPtr dynamicOffsetCount, IntPtr dynamicOffsets);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetVertexBuffer(IntPtr pass, uint slot, IntPtr buffer, ulong offset, ulong size);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetIndexBuffer(IntPtr pass, IntPtr buffer, uint format, ulong offset, ulong size);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetViewport(IntPtr pass, float x, float y, float width, float height, float minDepth, float maxDepth);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderSetScissorRect(IntPtr pass, uint x, uint y, uint width, uint height);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderDraw(IntPtr pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderDrawIndexed(IntPtr pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderDrawIndirect(IntPtr pass, IntPtr indirectBuffer, ulong indirectOffset);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderEnd(IntPtr pass);

        // ---- compute pass ----
        [DllImport(Library)] public static extern void wgpuComputePassEncoderSetPipeline(IntPtr pass, IntPtr pipeline);
        [DllImport(Library)] public static extern void wgpuComputePassEncoderSetBindGroup(IntPtr pass, uint groupIndex, IntPtr group, UIntPtr dynamicOffsetCount, IntPtr dynamicOffsets);
        [DllImport(Library)] public static extern void wgpuComputePassEncoderDispatchWorkgroups(IntPtr pass, uint x, uint y, uint z);
        [DllImport(Library)] public static extern void wgpuComputePassEncoderEnd(IntPtr pass);

        // ---- reference counting: one Release per object type ----
        [DllImport(Library)] public static extern void wgpuAdapterRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuDeviceRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuQueueRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuBufferRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuTextureRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuTextureViewRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuSamplerRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuShaderModuleRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuBindGroupLayoutRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuBindGroupRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuPipelineLayoutRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuRenderPipelineRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuComputePipelineRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuCommandEncoderRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuCommandBufferRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuRenderPassEncoderRelease(IntPtr o);
        [DllImport(Library)] public static extern void wgpuComputePassEncoderRelease(IntPtr o);
    }
}
