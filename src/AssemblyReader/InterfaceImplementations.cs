using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class InterfaceImplementations
    {
        // Return the table and its column sizes. Each row pairs a TypeDef
        // with the nominal TypeDefOrRef of an implemented interface.
        internal static int ReadTable(byte[] image, int[] heaps, int[] methods, int[] table)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            int cursor = methods[0] + counts[6] * methods[1];
            int limit = heaps[4] + heaps[5];
            int paramWidth = StandaloneSignatures.Width(counts, 8, methods[2], methods[3]);
            if (counts[8] > (limit - cursor) / paramWidth) return -1;
            cursor += counts[8] * paramWidth;
            int width = StandaloneSignatures.Width(counts, 9, methods[2], methods[3]);
            if (counts[9] > (limit - cursor) / width) return -1;
            table[0] = cursor;
            table[1] = counts[2] < 65536 ? 2 : 4;
            table[2] = width;
            return counts[9];
        }

        internal static int Read(byte[] image, int[] table, int index, int owner)
        {
            uint row = (uint)(table[0] + index * table[2]);
            int ownerWidth = table[1];
            if (ReadIndex(image, row, ownerWidth) != owner) return 0;
            int reference = ReadIndex(image, row + (uint)ownerWidth, table[2] - ownerWidth);
            return reference > 3 && (reference & 3) != 3 ? reference : -1;
        }

        internal static bool HasDefinitionReference(byte[] image, int[] heaps, int[] methods, int definition)
        {
            var table = new int[3];
            int count = ReadTable(image, heaps, methods, table);
            if (count < 0) return true; // Unknown implementers keep the conservative diagnostic.
            uint start = (uint)(table[0] + table[1]);
            int width = table[2] - table[1];
            for (int row = 0; row < count; row++)
                if (ReadIndex(image, start + (uint)(row * table[2]), width) == (definition << 2))
                    return true;
            return false;
        }

        private static int ReadIndex(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
