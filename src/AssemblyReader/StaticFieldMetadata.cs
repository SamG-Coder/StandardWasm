using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class StaticFieldMetadata
    {
        internal static int Check(byte[] image, int[] heaps, int[] layout, int row, int flags, bool write)
        {
            const int Static = 0x10, InitOnly = 0x20, Literal = 0x40, HasRva = 0x100,
                HasMarshal = 0x1000, HasDefault = 0x8000;
            if ((flags & Static) == 0 || (flags & (Literal | HasRva | HasMarshal | HasDefault)) != 0 ||
                (write && (flags & InitOnly) != 0)) return -2;

            // Nullable annotations describe compiler flow analysis. Other field attributes
            // may change storage lifetime, so keep rejecting those until they are modeled.
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            if (counts[12] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 12);
            if (table < 0) return -1;
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int width = StandaloneSignatures.Width(counts, 12, layout[2], layout[3]);
            int constructorWidth = width - parentWidth - layout[3];
            int parent = row << 5 | 1; // HasCustomAttribute.FieldDef.
            for (int i = 0; i < counts[12]; i++)
            {
                uint at = (uint)(table + i * width);
                int candidate = AttributeConstructors.ReadRowIndex(image, at, parentWidth);
                if (candidate != parent) continue;
                uint constructorAt = at + (uint)parentWidth;
                int constructor = AttributeConstructors.ReadRowIndex(image, constructorAt, constructorWidth);
                int type = AttributeConstructors.ReadType(image, heaps, layout, counts, constructor);
                if (type < 0) return type;
                if (!IsNullableAnnotation(image, heaps, layout, counts, first, type)) return -2;
            }
            return 0;
        }

        private static bool IsNullableAnnotation(byte[] image, int[] heaps, int[] layout, int[] counts, int first, int type)
        {
            if (type == 0) return false;
            int row = type >> 3;
            if ((type & 7) == 0)
                return ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row,
                    "System.Runtime.CompilerServices", "NullableAttribute");
            var reference = new int[AssemblyReferences.SymbolWidth];
            return AssemblyReferences.ReadType(image, heaps, layout, row, reference) >= 0 &&
                reference[AssemblyReferences.Depth] == 0 &&
                MatchesAscii(image, heaps, (uint)reference[2], "NullableAttribute") &&
                MatchesAscii(image, heaps, (uint)reference[3], "System.Runtime.CompilerServices");
        }
    }
}
