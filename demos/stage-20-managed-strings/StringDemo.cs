namespace StringLibrary
{
    public static class StringDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class Kernels
    {
        public static string Literal(int choice)
        {
            if (choice == 0) return "A\0\u03a9\ud83d\ude80";
            if (choice == 1) return "";
            if (choice == 2) return "012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
            return "\ud800x";
        }

        public static int Length(int choice) => Literal(choice).Length;
        public static int CodeUnit(int choice, int index) => Literal(choice)[index];
    }
}
