using NativeLayoutDependency;

namespace NativeLayout
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var cell = new NativeCell(19);
            cell.Signed = value;
            cell.Unsigned = unchecked((nuint)(uint)value);
            return unchecked((int)cell.Signed + (int)cell.Unsigned + cell.Marker);
        }

        public static long SignedRoundTrip(long value)
        {
            var cell = new NativeCell(19);
            cell.Signed = unchecked((nint)value);
            return cell.ReadSigned();
        }

        public static long UnsignedRoundTrip(long value)
        {
            var cell = new NativeCell(19);
            cell.Unsigned = unchecked((nuint)value);
            return unchecked((long)cell.ReadUnsigned());
        }

        public static long CheckedSigned(long value) => checked((nint)value);
        public static long CheckedUnsigned(ulong value) => unchecked((long)checked((nuint)value));

        public static int Compare(int value)
        {
            nint signed = value;
            nuint unsigned = unchecked((nuint)(uint)value);
            return (signed < (nint)0 ? 1 : 0) + (unsigned > (nuint)int.MaxValue ? 2 : 0);
        }

        public static int ArrayAt(int length, int index)
        {
            nint position = index;
            var bytes = new int[(nint)length];
            bytes[position] = 19;
            return bytes[position] + bytes.Length;
        }

        public static bool HasArrayItems(int length) => new int[length].Length != 0;

        public static nint RejectNativeAdd(nint left, nint right) => left + right;
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
