namespace NarrowScalars
{
    public static class ScalarCases
    {
        public static int ReadSigned(sbyte small, short medium) => small + medium;

        public static int ReadUnsigned(byte small, ushort medium, char character) => small + medium + character;

        public static sbyte WrapSigned(int value) => unchecked((sbyte)value);

        public static ushort WrapUnsigned(int value) => unchecked((ushort)value);

        public static int Compose(int value) => WrapSigned(value) + WrapUnsigned(value);

        public static int Count(int steps)
        {
            short value = 0;
            for (int i = 0; i < steps; i++) value = unchecked((short)(value + 1000));
            return value;
        }
    }

    public static class ScalarDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int method, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, method, output);
    }
}
