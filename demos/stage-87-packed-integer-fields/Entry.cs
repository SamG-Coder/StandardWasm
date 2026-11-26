using PackedFieldDependency;

namespace PackedFieldDemo
{
    public sealed class Derived : Register
    {
        public short Local;

        public Derived(int value) : base(value) => Local = unchecked((short)value);
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var row = new Derived(value);
            row.SignedByte = unchecked((sbyte)value);
            row.UnsignedByte = unchecked((byte)value);
            row.SignedShort = unchecked((short)value);
            row.UnsignedShort = unchecked((ushort)value);
            row.Character = unchecked((char)value);
            row.Flag = (value & 1) != 0;
            row.Word = unchecked((uint)value);
            return unchecked(row.SignedByte + row.UnsignedByte * 3 + row.SignedShort * 7 +
                row.UnsignedShort * 11 + row.Character * 13 + row.Local * 17 +
                (row.Flag ? 19 : 0) + row.Marker * 23 + (int)row.Word * 29);
        }

        public static long WidenWord(int value)
        {
            var row = new Register(0) { Word = unchecked((uint)value) };
            return row.Word;
        }

        public static int TruncateSigned(int value)
        {
            var row = new Register(0) { SignedByte = unchecked((sbyte)value) };
            return row.SignedByte;
        }

        public static int Store(int value)
        {
            var row = new Register(0) { UnsignedShort = NarrowValue(value) };
            return row.UnsignedShort;
        }

        private static ushort NarrowValue(int value) => unchecked((ushort)value);
        public static long WideValue(int value) => value;

        public static int NullRead(Register row) => row.SignedShort;
        public static void NullWrite(Register row, int value) => row.SignedShort = unchecked((short)value);
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
