using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MetadataTableRows
    {
        internal static int ReadCounts(byte[] image, int start, int size, int[] counts)
        {
            if (start < 0 || size < 24 || !Fits((uint)start, (uint)size, (uint)image.Length))
                return MethodDefinitions.Malformed;
            uint header = (uint)start;
            uint end = (uint)(start + size);
            uint heaps = image[header + 6];
            uint validLow = U32(image, header + 8);
            uint validHigh = U32(image, header + 12);
            // This reader handles ordinary ECMA compressed tables. Pointer and ENC
            // tables require different ownership rules. PDB tables use other layouts.
            const uint unsupportedTables = (1u << 3) | (1u << 5) | (1u << 7) |
                (1u << 19) | (1u << 22) | (1u << 30) | (1u << 31);
            if (image[header + 4] != 2 || (heaps & ~7u) != 0 ||
                (validLow & unsupportedTables) != 0 || (validHigh & ~0x1fffu) != 0)
                return MethodDefinitions.Unsupported;
            uint cursor = header + 24;
            for (int table = 0; table < counts.Length; table++)
            {
                uint mask = table < 32 ? validLow : validHigh;
                if ((mask & (1u << (table & 31))) == 0)
                    continue;
                if (!Fits(cursor, 4, end))
                    return MethodDefinitions.Malformed;
                uint count = U32(image, cursor);
                if (count > 0xffffff)
                    return MethodDefinitions.Malformed;
                counts[table] = (int)count;
                cursor += 4;
            }

            return (int)cursor;
        }
    }
}
