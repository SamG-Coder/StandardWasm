// Decision 0095: C# fixed buffers in native layouts. The x86 CLR is the oracle.
// Each buffer is a <name>e__FixedBuffer value whose ClassLayout.Size is its byte
// extent, aligned to its element. The compiler verifies this extent against
// the declaring field's FixedBufferAttribute before lowering native access.
public unsafe struct FixedBytes
{
    public fixed byte Data[5];
}

public unsafe struct FixedMix
{
    public byte Tag;
    public fixed char Text[3];
    public short AfterText;
    public fixed int Words[2];
    public fixed long Wide[1];
    public byte AfterWide;
    public fixed bool Flags[3];
    public fixed double Real[1];
    public int Last;
}

public static unsafe class FixedBufferGuest
{
    public static int FixedSizes() => sizeof(FixedBytes) * 1000 + sizeof(FixedMix);

    public static int FixedFieldAccess(nint address)
    {
        var mix = (FixedMix*)address;
        mix->Tag = 7;
        mix->AfterText = -2;
        mix->AfterWide = 9;
        mix->Last = 0x0a0b0c0d;
        byte* start = (byte*)mix;
        return (int)((byte*)&mix->AfterText - start) | (int)((byte*)&mix->AfterWide - start) << 8 |
            (int)((byte*)&mix->Last - start) << 16;
    }

    public static int FixedElementAccess(nint address) => ((FixedMix*)address)->Words[1];

    public static int FixedManagedLocal()
    {
        FixedBytes local;
        local.Data[0] = 1;
        return local.Data[0];
    }
}
