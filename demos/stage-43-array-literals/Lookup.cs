namespace ArrayLiterals
{
    public static class Lookup
    {
        public static int ByteAt(int index) => new byte[] { 255, 128, 17, 0, 233, 64, 7, 91 }[index];

        public static int ShortAt(int index) => new short[] { -32768, -19, 253, 32767, 101 }[index];

        public static int IntegerAt(int index) =>
            new int[] { int.MinValue, -93, 0, 7, int.MaxValue }[index];

        public static long LongAt(int index) =>
            new long[] { long.MinValue, -10500930170, 0, 748129347124, long.MaxValue }[index];

        public static float SingleAt(int index) =>
            new float[] { -0.0f, float.Epsilon, 1.25f, -3f, float.PositiveInfinity }[index];

        public static double DoubleAt(int index) =>
            new double[] { -0.0, double.Epsilon, 13.5, -3.25, double.NegativeInfinity }[index];

        public static int Mixed(int index)
        {
            var data = new byte[] { 201, 5, 7, 19, 39, 129, 121 };
            return data[index] + "ÁβZ"[index % 3];
        }

        public static int Fresh(int value)
        {
            var a = new int[] { 11, 23, -58, 420 };
            var b = new int[] { 11, 23, -58, 420 };
            a[1] = value;
            return a[1] + b[1];
        }

        public static int Conditional(int empty, int index)
        {
            if (empty != 0) return 0;
            return new int[] { 31, -5, 7, 11 }[index];
        }
    }

    public static class LiteralDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
