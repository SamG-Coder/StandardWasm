using PeLibrary;

namespace BodyLibrary
{
    public static class BodyDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];

        public static void SetByte(byte[] image, int index, int value) =>
            image[index] = unchecked((byte)value);

        public static int InspectRow(byte[] image, int row, int field)
        {
            var methods = new int[4];
            int count = CompressedTables.Find(image, methods);
            if (count < 0)
                return count;
            if (row < 1 || row > count)
                return MethodBodies.Malformed;
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 0));
            return InspectRva(image, rva, field);
        }

        // Field -1 returns status. Fields 0..5 select layout values.
        // Subsequent fields read individual IL bytes as verified by the fixture.
        public static int InspectRva(byte[] image, uint rva, int field)
        {
            var body = new int[6];
            int status = MethodBodies.Read(image, rva, body);
            if (status != 0 || field == -1)
                return status;
            if (field >= 0 && field < body.Length)
                return body[field];
            int index = field - body.Length;
            if (index < 0 || index >= body[1])
                return MethodBodies.Malformed;
            return image[body[0] + index];
        }
    }
}
