using ReferenceLeaf;

namespace ReferenceBridge
{
    public static class Bridge
    {
        public static byte[] Edit(byte[] data, int index, int value)
        {
            byte[] alias = Leaf.Write(data, index, unchecked((byte)value));
            alias[1] = 9;
            return alias;
        }

        public static int Unsigned(byte[] data) => Leaf.Read(data);
        public static int Signed(sbyte[] data) => Leaf.Read(data);

        public static int[] Literal() => Leaf.Literal();

        public static string MakeLabel(int value, int variant)
        {
            var units = new char[2];
            units[0] = '[';
            units[1] = unchecked((char)value);
            string suffix = variant == 0 ? "é" : "😀";
            return Leaf.Format(units, suffix);
        }

        public static int CodeUnit(string value, int index) => Leaf.CodeUnit(value, index);
    }
}
