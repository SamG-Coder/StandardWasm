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
            if (choice == 3) return "\ud800x";
            if (choice == 4) return "\ud800y";
            return null;
        }

        public static int Length(int choice) => Literal(choice).Length;
        public static int CodeUnit(int choice, int index) => Literal(choice)[index];

        // String.op_Equality and String.op_Inequality compare ordinally; a
        // choice outside 0..4 selects null. Bit 0 is ==, bit 1 is !=.
        public static int Equality(int left, int right)
        {
            string a = Literal(left), b = Literal(right);
            return (a == b ? 1 : 0) | (a != b ? 2 : 0);
        }
    }
}
