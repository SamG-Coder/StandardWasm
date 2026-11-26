namespace ReferenceLeaf
{
    public static class Leaf
    {
        public static byte[] Write(byte[] data, int index, byte value)
        {
            data[index] = value;
            return data;
        }

        public static int Read(byte[] data) => data[0] + 11;
        public static int Read(sbyte[] data) => data[0] - 13;

        public static int[] Literal() => new[] { 2, 4, 8, 16, 32, 64, 128, 256 };

        public static string Format(char[] units, string suffix) =>
            string.Concat(new string(units), suffix);

        public static int CodeUnit(string value, int index) => value[index];
    }
}
