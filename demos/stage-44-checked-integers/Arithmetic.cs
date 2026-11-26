namespace CheckedIntegers
{
    public static class Arithmetic
    {
        public static int AddI32(int a, int b) => checked(a + b);
        public static int SubI32(int a, int b) => checked(a - b);
        public static int MulI32(int a, int b) => checked(a * b);
        public static uint AddU32(uint a, uint b) => checked(a + b);
        public static uint SubU32(uint a, uint b) => checked(a - b);
        public static uint MulU32(uint a, uint b) => checked(a * b);
        public static long AddI64(long a, long b) => checked(a + b);
        public static long SubI64(long a, long b) => checked(a - b);
        public static long MulI64(long a, long b) => checked(a * b);
        public static ulong AddU64(ulong a, ulong b) => checked(a + b);
        public static ulong SubU64(ulong a, ulong b) => checked(a - b);
        public static ulong MulU64(ulong a, ulong b) => checked(a * b);
    }

    public static class CheckedDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
