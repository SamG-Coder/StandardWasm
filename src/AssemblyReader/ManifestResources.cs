using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ManifestResources
    {
        private const int ManifestResourceTable = 40;

        // Find an ordinal UTF-8 name in an already validated managed image.
        // Returns an image-backed byte range, with no copy. Missing names (-4)
        // and externally implemented resources (-2) have distinct outcomes.
        internal static int Find(byte[] image, int[] streams, int[] methods, byte[] name, int[] location)
        {
            var counts = new int[45];
            int status = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (status < 0) return status;
            int count = counts[ManifestResourceTable];
            if (count == 0 || name.Length == 0) return -4;

            int start = MetadataTableRows.TableStart(streams, methods, counts, ManifestResourceTable);
            if (start < 0) return -1;
            int stringWidth = methods[2], implementationWidth = MetadataTableRows.ImplementationWidth(counts);
            int rowWidth = 8 + stringWidth + implementationWidth, found = -1;
            for (int i = 0; i < count; i++)
            {
                int row = start + i * rowWidth;
                uint nameIndex = ReadIndex(image, row + 8, stringWidth);
                if (nameIndex == 0 || nameIndex >= (uint)streams[3]) return -1;
                int available = streams[3] - (int)nameIndex;
                int text = streams[2] + (int)nameIndex;
                if (name.Length >= available || !NameMatches(image, text, name)) continue;
                if (found >= 0) return -1;
                found = row;
            }
            if (found < 0) return -4;
            if (ReadIndex(image, found + 8 + stringWidth, implementationWidth) != 0) return -2;

            var directory = new int[2];
            status = PeLocator.LocateResources(image, directory);
            if (status != 0) return -1; // A declared embedded row requires a payload.
            uint offset = U32(image, (uint)found);
            uint size = (uint)directory[1];
            if (!Fits(offset, 4, size)) return -1;
            uint prefix = (uint)directory[0] + offset;
            uint length = U32(image, prefix);
            if (!Fits(offset + 4, length, size)) return -1;
            location[0] = (int)(prefix + 4);
            location[1] = (int)length;
            return 0;
        }

        private static bool NameMatches(byte[] image, int start, byte[] name)
        {
            if (image[start + name.Length] != 0) return false;
            for (int i = 0; i < name.Length; i++)
                if (name[i] == 0 || name[i] != image[start + i]) return false;
            return true;
        }

        private static uint ReadIndex(byte[] image, int offset, int width) =>
            width == 2 ? U16(image, (uint)offset) : U32(image, (uint)offset);
    }
}
