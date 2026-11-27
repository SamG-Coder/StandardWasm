extern alias foreign;

using System.Runtime.Intrinsics;

public static class VectorCountProbe
{
    public static int SignedByte() => Vector128<sbyte>.Count;
    public static int UnsignedByte() => Vector128<byte>.Count;
    public static int SignedWord() => Vector128<short>.Count;
    public static int UnsignedWord() => Vector128<ushort>.Count;
    public static int SignedDword() => Vector128<int>.Count;
    public static int UnsignedDword() => Vector128<uint>.Count;
    public static int SignedQword() => Vector128<long>.Count;
    public static int UnsignedQword() => Vector128<ulong>.Count;
    public static int Single() => Vector128<float>.Count;
    public static int Double() => Vector128<double>.Count;
    public static int NativeSigned() => Vector128<nint>.Count;
    public static int NativeUnsigned() => Vector128<nuint>.Count;

    public static int Generic() => Count<ushort>();
    private static int Count<T>() => Vector128<T>.Count;
    public static int ForeignType() => foreign::System.Runtime.Intrinsics.Vector128<ushort>.Count;

    public static int InvalidChar() => Vector128<char>.Count;
    public static int InvalidBoolean() => Vector128<bool>.Count;
    public static int InvalidEnum() => Vector128<Word>.Count;

    private enum Word : ushort { Zero }
}
