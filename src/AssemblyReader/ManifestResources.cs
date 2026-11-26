using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ManifestResources
    {
        private const int ManifestResourceTable = 40;
        internal const int CatalogWidth = 6;
        private const int Start = 0, Count = 1, RowWidth = 2, NameWidth = 3, Strings = 4, StringBytes = 5;

        // Open the metadata table without reading resource bytes. A linked
        // image can name resources even when it has no embedded directory.
        internal static int Open(byte[] image, int[] streams, int[] methods, int[] catalog)
        {
            var counts = new int[45];
            int status = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (status < 0) return status;
            int count = counts[ManifestResourceTable];
            int start = count == 0 ? 0 : MetadataTableRows.TableStart(streams, methods, counts, ManifestResourceTable);
            if (start < 0) return -1;
            int stringWidth = methods[2];
            catalog[Start] = start;
            catalog[Count] = count;
            catalog[RowWidth] = 8 + stringWidth + MetadataTableRows.ImplementationWidth(counts);
            catalog[NameWidth] = stringWidth;
            catalog[Strings] = streams[2];
            catalog[StringBytes] = streams[3];
            return count;
        }

        // Read the UTF-8 name of the zero-based resource row.
        internal static int Name(byte[] image, int[] catalog, int row, int[] range)
        {
            if ((uint)row >= (uint)catalog[Count]) return -1;
            int entry = catalog[Start] + row * catalog[RowWidth];
            uint index = ReadIndex(image, entry + 8, catalog[NameWidth]);
            if (index == 0 || index >= (uint)catalog[StringBytes]) return -1;
            int start = catalog[Strings] + (int)index;
            int limit = catalog[Strings] + catalog[StringBytes], end = start;
            while (end < limit && image[end] != 0) end++;
            if (end == limit || end == start) return -1;
            range[0] = start;
            range[1] = end - start;
            return 0;
        }

        // Runtime Strings use UTF-16 code units. Keep resource-name lookup
        // ordinal across the metadata encoding boundary.
        internal static int CopyName(byte[] image, int[] catalog, int row, byte[] output, int offset)
        {
            var range = new int[2];
            int status = Name(image, catalog, row, range);
            return status < 0 ? status : Utf8Scalars.CopyUtf16(image, range[0], range[1], output, offset);
        }

        // Only embedded entries have a file-backed payload in this image.
        // The caller can share a previously validated directory across rows.
        internal static int Data(byte[] image, int[] catalog, int row, int[] directory, int[] range)
        {
            if ((uint)row >= (uint)catalog[Count]) return -1;
            int entry = catalog[Start] + row * catalog[RowWidth];
            int implementation = 8 + catalog[NameWidth];
            if (ReadIndex(image, entry + implementation, catalog[RowWidth] - implementation) != 0) return -2;
            if (directory[1] == 0 && PeLocator.LocateResources(image, directory) != 0) return -1;

            uint offset = U32(image, (uint)entry), size = (uint)directory[1];
            if (!Fits(offset, 4, size)) return -1;
            uint prefix = (uint)directory[0] + offset;
            uint length = U32(image, prefix);
            if (!Fits(offset + 4, length, size)) return -1;
            range[0] = (int)(prefix + 4);
            range[1] = (int)length;
            return 0;
        }

        // Returns the original bytes for an ordinal name without allocating a
        // copy. Missing names (-4) and external resources (-2) remain distinct.
        internal static int Find(byte[] image, int[] streams, int[] methods, byte[] name, int[] location)
        {
            var catalog = new int[CatalogWidth];
            int count = Open(image, streams, methods, catalog);
            if (count < 0) return count;
            if (count == 0 || name.Length == 0) return -4;
            int found = -1;
            var range = new int[2];
            for (int row = 0; row < count; row++)
            {
                int status = Name(image, catalog, row, range);
                if (status < 0) return status;
                if (name.Length != range[1] || !NameMatches(image, range[0], name)) continue;
                if (found >= 0) return -1;
                found = row;
            }
            return found < 0 ? -4 : Data(image, catalog, found, new int[2], location);
        }

        private static bool NameMatches(byte[] image, int start, byte[] name)
        {
            for (int i = 0; i < name.Length; i++)
                if (name[i] != image[start + i]) return false;
            return true;
        }

        private static uint ReadIndex(byte[] image, int offset, int width) =>
            width == 2 ? U16(image, (uint)offset) : U32(image, (uint)offset);
    }
}
