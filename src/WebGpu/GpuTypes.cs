using System;

namespace StandardWasm.WebGpu
{
    // Flag bits are the webgpu.h bits and cross to the native backend unchanged (decision 0096).

    [Flags]
    public enum GpuBufferUsage : ulong
    {
        None = 0,
        MapRead = 0x1,
        MapWrite = 0x2,
        CopySrc = 0x4,
        CopyDst = 0x8,
        Index = 0x10,
        Vertex = 0x20,
        Uniform = 0x40,
        Storage = 0x80,
        Indirect = 0x100,
        QueryResolve = 0x200,
    }

    [Flags]
    public enum GpuTextureUsage : ulong
    {
        None = 0,
        CopySrc = 0x1,
        CopyDst = 0x2,
        TextureBinding = 0x4,
        StorageBinding = 0x8,
        RenderAttachment = 0x10,
    }

    [Flags]
    public enum GpuShaderStage : ulong
    {
        None = 0,
        Vertex = 0x1,
        Fragment = 0x2,
        Compute = 0x4,
    }

    [Flags]
    public enum GpuColorWriteMask : ulong
    {
        None = 0,
        Red = 0x1,
        Green = 0x2,
        Blue = 0x4,
        Alpha = 0x8,
        All = 0xF,
    }

    // Enum ordinals below are managed values. The native backend maps each one explicitly against
    // its pinned webgpu.h release; they are never cast across the boundary.

    public enum GpuTextureFormat
    {
        Undefined,
        R8Unorm,
        Rg8Unorm,
        R16Float,
        R32Float,
        R32Uint,
        Rg16Float,
        Rgba8Unorm,
        Rgba8UnormSrgb,
        Bgra8Unorm,
        Bgra8UnormSrgb,
        Rgba16Float,
        Rgba32Float,
        Depth24Plus,
        Depth32Float,
    }

    public enum GpuTextureDimension { D1, D2, D3 }

    public enum GpuVertexFormat
    {
        Float32,
        Float32x2,
        Float32x3,
        Float32x4,
        Uint32,
        Uint32x2,
        Uint32x4,
        Unorm8x4,
    }

    public enum GpuVertexStepMode { Vertex, Instance }

    public enum GpuPrimitiveTopology { PointList, LineList, LineStrip, TriangleList, TriangleStrip }

    public enum GpuIndexFormat { Uint16, Uint32 }

    public enum GpuFrontFace { Ccw, Cw }

    public enum GpuCullMode { None, Front, Back }

    public enum GpuCompareFunction { Never, Less, Equal, LessEqual, Greater, NotEqual, GreaterEqual, Always }

    public enum GpuLoadOp { Clear, Load }

    public enum GpuStoreOp { Store, Discard }

    public enum GpuFilterMode { Nearest, Linear }

    public enum GpuAddressMode { ClampToEdge, Repeat, MirrorRepeat }

    public enum GpuBufferBindingType { Uniform, Storage, ReadOnlyStorage }

    public enum GpuBlendFactor { Zero, One, Src, OneMinusSrc, SrcAlpha, OneMinusSrcAlpha, Dst, OneMinusDst, DstAlpha, OneMinusDstAlpha }

    public enum GpuBlendOperation { Add, Subtract, ReverseSubtract, Min, Max }

    public enum GpuPowerPreference { Undefined, LowPower, HighPerformance }

    /// <summary>An RGBA clear colour in linear double precision, as in webgpu.h's WGPUColor.</summary>
    public struct GpuColor
    {
        public double R, G, B, A;
        public GpuColor(double r, double g, double b, double a) { R = r; G = g; B = b; A = a; }
    }
}
