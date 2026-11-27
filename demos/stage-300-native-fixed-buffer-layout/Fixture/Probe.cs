#pragma warning disable CS0649 // These fields describe metadata layout for sizeof.
using System.Runtime.InteropServices;

public static class Probe
{
    private unsafe struct ByteCarrier
    {
        public byte Prefix;
        public fixed byte Payload[17];
        public uint Footer;
    }

    private unsafe struct IntCarrier
    {
        public short Prefix;
        public fixed int Payload[3];
        public byte Suffix;
    }

    private struct NestedCarrier
    {
        public byte Prefix;
        public ByteCarrier Inner;
        public int Suffix;
    }

    [StructLayout(LayoutKind.Sequential, Size = 17)]
    private struct NonAttributed
    {
        public byte First;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct Packed
    {
        public byte Prefix;
        public fixed byte Payload[17];
        public uint Footer;
    }

    public static unsafe int ByteSize() => sizeof(ByteCarrier);

    public static unsafe int IntSize() => sizeof(IntCarrier);

    public static unsafe int NestedSize() => sizeof(NestedCarrier);

    public static unsafe int UnverifiedSize() => sizeof(NonAttributed);

    public static unsafe int ExplicitPacking() => sizeof(Packed);
}
