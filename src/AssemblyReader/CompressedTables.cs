using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class CompressedTables
    {
        internal static int Find(byte[] image, int[] layout)
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
            return MethodDefinitions.ReadLayout(image, streams[compressed + 2], streams[compressed + 3], layout);
        }
    }
}
