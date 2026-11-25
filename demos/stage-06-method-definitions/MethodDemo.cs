using PeLibrary;

namespace MethodLibrary
{
    public static class MethodDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];

        public static void SetByte(byte[] image, int index, int value) =>
            image[index] = unchecked((byte)value);

        public static int HasMethods(byte[] image) => Inspect(image, 0, 0) > 0 ? 1 : 0;

        // Row IDs are one-based. Row zero requests the table's layout or count.
        public static int Inspect(byte[] image, int row, int field)
        {
            var layout = new int[4];
            int count = CompressedTables.Find(image, layout);
            if (count < 0)
                return count;
            if (row == 0)
                return field == 0 ? count : layout[field - 1];
            if (row < 0 || row > count)
                return MethodDefinitions.Malformed;
            return MethodDefinitions.ReadColumn(image, layout, row, field);
        }

        public static int InspectRawStream(byte[] stream, int field)
        {
            var layout = new int[4];
            int count = MethodDefinitions.ReadLayout(stream, 0, stream.Length, layout);
            if (count < 0 || field == 0)
                return count;
            return layout[field - 1];
        }

    }
}
