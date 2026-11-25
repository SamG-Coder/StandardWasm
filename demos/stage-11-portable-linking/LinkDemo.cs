namespace LinkLibrary
{
    public static class LinkDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class LinkKernels
    {
        public static int CombinePaths(int value) => Left(value) + Right(value);
        private static int Left(int value) => Shared(value, 3) + Bias();
        private static int Right(int value) => Shared(value, 5) - Bias();
        private static int Shared(int value, int multiplier) => value * multiplier;
        private static int Bias() => 17;

        public static int Recur(int value) => (value & 31) + EvenStep(value & 31);
        private static int EvenStep(int value) => value <= 0 ? 1 : OddStep(value - 1);
        private static int OddStep(int value) => value <= 0 ? 0 : EvenStep(value - 1);

        public static int Terminal(int value) => value + 7;
    }
}
