namespace StreamLibrary
{
    public static class StreamDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];

        public static void SetByte(byte[] image, int index, int value) =>
            image[index] = unchecked((byte)value);

        public static int Inspect(byte[] image, int catalogSize, int field)
        {
            var location = new int[2];
            int status = PeLibrary.PeLocator.LocateMetadata(image, location);
            if (status != 0)
                return -100 - status;
            var catalog = new int[catalogSize];
            if (catalogSize > 0)
                catalog[0] = -17;
            int count = PeLibrary.MetadataStreams.Read(image, location[0], location[1], catalog);
            if (field < 0)
                return count;
            return catalog[field];
        }
    }
}
