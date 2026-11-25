namespace TypedArrayLibrary
{
    public static class ArrayDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class ArrayKernels
    {
        public static int Route(int input, int count)
        {
            var bytes = MakeBytes(count);
            var ints = new int[count];
            Write(bytes, 0, input);
            Store(ints, 0, input);
            byte[] selected;
            if (input >= 0)
                selected = bytes;
            else
                selected = MakeBytes(count);
            return Read(selected) + (ints[0] & 1023) + bytes.Length + ints.Length;
        }

        private static byte[] MakeBytes(int count) => new byte[count];
        private static void Write(byte[] bytes, int index, int value) => bytes[index] = unchecked((byte)value);
        private static void Store(int[] values, int index, int value) => values[index] = value;
        private static int Read(byte[] bytes) => bytes[0];
    }
}
