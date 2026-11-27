namespace NativeUnary;

public static class Probe
{
    public static int PositiveComplement() => unchecked((int)Complement(17));
    public static int ZeroComplement() => unchecked((int)Complement(0));
    public static int LocalZeroComplement()
    {
        nint zero = 0;
        return unchecked((int)~zero);
    }
    public static int NegativeComplement() => unchecked((int)Complement(int.MinValue));
    public static int UnsignedComplement() => unchecked((int)ComplementUnsigned(0x80000000u));
    public static int NegativeWord() => unchecked((int)Negate(17));
    public static int MinimumNegation() => unchecked((int)Negate(int.MinValue));
    public static int ClearTaggedWord() => unchecked((int)WithoutTag(unchecked((int)0x80000003u), 1));

    private static nint Complement(nint value) => ~value;
    private static nuint ComplementUnsigned(nuint value) => ~value;
    private static nint Negate(nint value) => unchecked(-value);
    private static nint WithoutTag(nint word, nint tag) => word & ~tag;
}
