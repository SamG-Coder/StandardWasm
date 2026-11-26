using System.Runtime.InteropServices;

namespace NestedOverlayDependency
{
    [StructLayout(LayoutKind.Auto)]
    public struct Pair<T> where T : struct
    {
        public T First;
        public T Second;
    }

    public struct Color
    {
        public int Argb;
        public short Known;
        public short State;
    }

    public struct Middle
    {
        public ushort First;
        public short Second;
        public ushort Third;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Overlay
    {
        [FieldOffset(0)] public long Raw;
        [FieldOffset(0)] public Pair<int> Pair;
        [FieldOffset(0)] public Color Color;
        [FieldOffset(1)] public Middle Middle;
    }

    public struct Padded
    {
        public byte First;
        public int Second;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Mixed
    {
        public short First;
        public int Second;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PaddedOverlay
    {
        [FieldOffset(0)] public Padded Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MixedOverlay
    {
        [FieldOffset(0)] public Mixed Value;
    }
}
