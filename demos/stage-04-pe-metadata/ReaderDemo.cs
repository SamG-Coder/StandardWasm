namespace PeLibrary
{
    public static class ReaderDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];

        public static void SetByte(byte[] image, int index, int value) =>
            image[index] = unchecked((byte)value);

        public static int Inspect(byte[] image, int field)
        {
            var location = new int[2];
            location[0] = -17;
            location[1] = -29;
            int status = PeLocator.LocateMetadata(image, location);
            if (field == 0)
                return status;
            return location[field - 1];
        }
    }
}
