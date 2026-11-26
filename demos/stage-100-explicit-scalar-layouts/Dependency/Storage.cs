using System.Runtime.InteropServices;

namespace ScalarOverlayDependency
{
    [StructLayout(LayoutKind.Explicit)]
    public struct Overlay
    {
        [FieldOffset(0)] public int Full;
        [FieldOffset(1)] public short Middle;
        [FieldOffset(3)] public sbyte High;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 1)]
    public struct FloatOverlay
    {
        [FieldOffset(1)] public int Bits;
        [FieldOffset(1)] public float Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DoubleOverlay
    {
        [FieldOffset(0)] public long Bits;
        [FieldOffset(0)] public double Value;
        [FieldOffset(3)] public int CrossWord;
        [FieldOffset(7)] public byte High;
    }

    public sealed class Frame
    {
        public Overlay Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct Large
    {
        [FieldOffset(0)] public int Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Nested
    {
        [FieldOffset(0)] public Overlay Value;
    }

    public sealed class LargeFrame
    {
        public Large Value;
    }

    public sealed class NestedFrame
    {
        public Nested Value;
    }
}
