namespace FlowLibrary
{
    public static class FlowDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class FlowKernels
    {
        public static int Fold(int count, int seed)
        {
            int total = seed;
            for (int i = 0; i < count; i++)
            {
                if ((i & 1) == 0)
                    total += i;
                else
                    total -= i;
            }
            return total;
        }

        public static int Select(int a, int b, int c) => a + (b < c ? b * 3 : c * 5);

        public static int UnsignedChoice(int a, int b) => (uint)a >= (uint)b ? a : b;
    }
}
