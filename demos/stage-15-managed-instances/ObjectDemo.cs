namespace ObjectLibrary
{
    public static class ObjectDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public sealed class Counter
    {
        private int count;
        private int stride;

        public Counter(int seed, int step)
        {
            count = seed;
            stride = step;
        }

        public int Advance()
        {
            count = unchecked(count + stride);
            return count;
        }

        public int Read() => count;
    }

    public sealed class Other
    {
        public int Value;
        public int Read() => Value;
    }

    public static class Kernels
    {
        public static int Isolated(int seed)
        {
            var first = new Counter(seed, 1);
            var second = new Counter(9, -2);
            first.Advance();
            second.Advance();
            return unchecked(first.Advance() + second.Read());
        }

        public static int SetOther(Other state, int value)
        {
            state.Value = value;
            return state.Value;
        }
    }
}
