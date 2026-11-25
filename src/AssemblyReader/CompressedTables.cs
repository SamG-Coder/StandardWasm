using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class CompressedTables
    {
        internal static int Find(byte[] image, int[] layout) =>
            FindWithHeaps(image, layout, new int[6]);

        internal static int FindWithHeaps(byte[] image, int[] layout, int[] heaps)
        {
            var root = new int[2];
            if (PeLocator.LocateMetadata(image, root) != 0)
                return MethodDefinitions.Malformed;
            int directory = MetadataStreams.ReadDirectoryStart(image, root[0], root[1]);
            if (directory < 0)
                return MethodDefinitions.Malformed;
            int[] streams = new int[(int)U16(image, (uint)directory - 2) * MetadataStreams.RecordWidth];
            int count = MetadataStreams.Read(image, root[0], root[1], streams);
            if (count < 0)
                return MethodDefinitions.Malformed;

            int compressed = -1, uncompressed = -1;
            for (int i = 0; i < count; i++)
            {
                int slot = i * MetadataStreams.RecordWidth;
                int name = streams[slot];
                // Compare the names #Blob and #Strings as little-endian ASCII words.
                if (streams[slot + 1] == 5 && U32(image, (uint)name) == 0x6f6c4223 && image[name + 4] == 'b')
                {
                    heaps[0] = streams[slot + 2];
                    heaps[1] = streams[slot + 3];
                }
                if (streams[slot + 1] == 8 && U32(image, (uint)name) == 0x72745323 &&
                    U32(image, (uint)name + 4) == 0x73676e69)
                {
                    heaps[2] = streams[slot + 2];
                    heaps[3] = streams[slot + 3];
                }
                if (streams[slot + 1] != 2 || image[name] != '#')
                    continue;
                if (image[name + 1] == '~')
                    compressed = slot;
                else if (image[name + 1] == '-')
                    uncompressed = slot;
            }
            if (uncompressed >= 0)
                return MethodDefinitions.Unsupported;
            if (compressed < 0)
                return MethodDefinitions.Missing;
            heaps[4] = streams[compressed + 2];
            heaps[5] = streams[compressed + 3];
            return MethodDefinitions.ReadLayout(image, heaps[4], heaps[5], layout);
        }
    }
}
