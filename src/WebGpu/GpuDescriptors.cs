namespace StandardWasm.WebGpu
{
    // Descriptors are plain managed classes (decision 0096). Applications never see a native
    // layout; each backend marshals them its own way (header-layout structs for webgpu.h, a log
    // entry for the recording backend).

    public sealed class GpuBufferDescriptor
    {
        public string Label;
        public ulong Size;
        public GpuBufferUsage Usage;
        public bool MappedAtCreation;
    }

    public sealed class GpuTextureDescriptor
    {
        public string Label;
        public uint Width;
        public uint Height;
        public uint DepthOrArrayLayers = 1;
        public uint MipLevelCount = 1;
        public uint SampleCount = 1;
        public GpuTextureDimension Dimension = GpuTextureDimension.D2;
        public GpuTextureFormat Format;
        public GpuTextureUsage Usage;
    }

    /// <summary>
    /// A view of part of a texture: one array layer or mip level to render into, or the whole
    /// array to sample. Null fields and zero counts mean "as the texture" (WebGPU defaults).
    /// </summary>
    public sealed class GpuTextureViewDescriptor
    {
        public string Label;
        /// <summary>Null: the texture's format.</summary>
        public GpuTextureFormat? Format;
        /// <summary>Null: the texture's own dimension (2D, or 2D array when it has layers).</summary>
        public GpuTextureViewDimension? Dimension;
        public GpuTextureAspect Aspect = GpuTextureAspect.All;
        public uint BaseMipLevel;
        /// <summary>0: every level from BaseMipLevel.</summary>
        public uint MipLevelCount;
        public uint BaseArrayLayer;
        /// <summary>0: every layer from BaseArrayLayer.</summary>
        public uint ArrayLayerCount;
    }

    public sealed class GpuSamplerDescriptor
    {
        public string Label;
        public GpuAddressMode AddressModeU = GpuAddressMode.ClampToEdge;
        public GpuAddressMode AddressModeV = GpuAddressMode.ClampToEdge;
        public GpuAddressMode AddressModeW = GpuAddressMode.ClampToEdge;
        public GpuFilterMode MagFilter = GpuFilterMode.Nearest;
        public GpuFilterMode MinFilter = GpuFilterMode.Nearest;
        public GpuFilterMode MipmapFilter = GpuFilterMode.Nearest;
        public float LodMinClamp;
        public float LodMaxClamp = 32f;
        public ushort MaxAnisotropy = 1;
    }

    public sealed class GpuShaderModuleDescriptor
    {
        public string Label;
        /// <summary>WGSL source. WGSL is the only shading language on every webgpu.h backend.</summary>
        public string Wgsl;
    }

    // ---- bind groups ----

    public sealed class GpuBindGroupLayoutEntry
    {
        public uint Binding;
        public GpuShaderStage Visibility;
        /// <summary>Exactly one of Buffer, Sampler, Texture or StorageTexture is set.</summary>
        public GpuBufferBindingLayout Buffer;
        public GpuSamplerBindingLayout Sampler;
        public GpuTextureBindingLayout Texture;
        public GpuStorageTextureBindingLayout StorageTexture;
    }

    public sealed class GpuBufferBindingLayout
    {
        public GpuBufferBindingType Type = GpuBufferBindingType.Uniform;
        public bool HasDynamicOffset;
        public ulong MinBindingSize;
    }

    public sealed class GpuSamplerBindingLayout
    {
        public GpuSamplerBindingType Type = GpuSamplerBindingType.Filtering;
    }

    public sealed class GpuTextureBindingLayout
    {
        public GpuTextureSampleType SampleType = GpuTextureSampleType.Float;
        public GpuTextureViewDimension ViewDimension = GpuTextureViewDimension.D2;
        public bool Multisampled;
    }

    public sealed class GpuStorageTextureBindingLayout
    {
        public GpuStorageTextureAccess Access = GpuStorageTextureAccess.WriteOnly;
        public GpuTextureFormat Format;
        public GpuTextureViewDimension ViewDimension = GpuTextureViewDimension.D2;
    }

    public sealed class GpuBindGroupLayoutDescriptor
    {
        public string Label;
        public GpuBindGroupLayoutEntry[] Entries;
    }

    public sealed class GpuBindGroupEntry
    {
        public uint Binding;
        /// <summary>Exactly one of Buffer, Sampler or TextureView is set.</summary>
        public GpuBuffer Buffer;
        public ulong Offset;
        /// <summary>0 means the rest of the buffer after Offset (WGPU_WHOLE_SIZE).</summary>
        public ulong Size;
        public GpuSampler Sampler;
        public GpuTextureView TextureView;
    }

    public sealed class GpuBindGroupDescriptor
    {
        public string Label;
        public GpuBindGroupLayout Layout;
        public GpuBindGroupEntry[] Entries;
    }

    public sealed class GpuPipelineLayoutDescriptor
    {
        public string Label;
        public GpuBindGroupLayout[] BindGroupLayouts;
    }

    // ---- pipelines ----

    public sealed class GpuVertexAttribute
    {
        public GpuVertexFormat Format;
        public ulong Offset;
        public uint ShaderLocation;
    }

    public sealed class GpuVertexBufferLayout
    {
        public ulong ArrayStride;
        public GpuVertexStepMode StepMode = GpuVertexStepMode.Vertex;
        public GpuVertexAttribute[] Attributes;
    }

    public sealed class GpuVertexState
    {
        public GpuShaderModule Module;
        public string EntryPoint = "vs_main";
        /// <summary>May be empty: a renderer that builds geometry from vertex_index needs none.</summary>
        public GpuVertexBufferLayout[] Buffers = new GpuVertexBufferLayout[0];
    }

    public sealed class GpuBlendComponent
    {
        public GpuBlendOperation Operation = GpuBlendOperation.Add;
        public GpuBlendFactor SrcFactor = GpuBlendFactor.One;
        public GpuBlendFactor DstFactor = GpuBlendFactor.Zero;
    }

    public sealed class GpuColorTargetState
    {
        public GpuTextureFormat Format;
        /// <summary>Null disables blending.</summary>
        public GpuBlendComponent BlendColor;
        public GpuBlendComponent BlendAlpha;
        public GpuColorWriteMask WriteMask = GpuColorWriteMask.All;
    }

    public sealed class GpuFragmentState
    {
        public GpuShaderModule Module;
        public string EntryPoint = "fs_main";
        public GpuColorTargetState[] Targets;
    }

    public sealed class GpuPrimitiveState
    {
        public GpuPrimitiveTopology Topology = GpuPrimitiveTopology.TriangleList;
        public GpuFrontFace FrontFace = GpuFrontFace.Ccw;
        public GpuCullMode CullMode = GpuCullMode.None;
    }

    public sealed class GpuDepthStencilState
    {
        public GpuTextureFormat Format;
        public bool DepthWriteEnabled = true;
        public GpuCompareFunction DepthCompare = GpuCompareFunction.Less;
    }

    public sealed class GpuMultisampleState
    {
        public uint Count = 1;
        public uint Mask = 0xFFFFFFFF;
        /// <summary>Alpha to coverage: thin blades fade without sorting when Count is above 1.</summary>
        public bool AlphaToCoverageEnabled;
    }

    public sealed class GpuRenderPipelineDescriptor
    {
        public string Label;
        /// <summary>Null asks for an automatic layout ("auto").</summary>
        public GpuPipelineLayout Layout;
        public GpuVertexState Vertex;
        public GpuPrimitiveState Primitive = new GpuPrimitiveState();
        public GpuDepthStencilState DepthStencil;
        public GpuMultisampleState Multisample = new GpuMultisampleState();
        public GpuFragmentState Fragment;
    }

    public sealed class GpuComputePipelineDescriptor
    {
        public string Label;
        public GpuPipelineLayout Layout;
        public GpuShaderModule Module;
        public string EntryPoint = "cs_main";
    }

    // ---- passes ----

    public sealed class GpuRenderPassColorAttachment
    {
        public GpuTextureView View;
        public GpuTextureView ResolveTarget;
        public GpuLoadOp LoadOp = GpuLoadOp.Clear;
        public GpuStoreOp StoreOp = GpuStoreOp.Store;
        public GpuColor ClearValue;
    }

    public sealed class GpuRenderPassDepthStencilAttachment
    {
        public GpuTextureView View;
        public GpuLoadOp DepthLoadOp = GpuLoadOp.Clear;
        public GpuStoreOp DepthStoreOp = GpuStoreOp.Store;
        public float DepthClearValue = 1f;
    }

    public sealed class GpuRenderPassDescriptor
    {
        public string Label;
        public GpuRenderPassColorAttachment[] ColorAttachments;
        public GpuRenderPassDepthStencilAttachment DepthStencilAttachment;
    }
}
