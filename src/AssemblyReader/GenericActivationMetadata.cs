using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class GenericActivationMetadata
    {
        internal static bool IsCoreWrapper(byte[] image, int[] layout, int[] heaps, int row)
        {
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            if ((flags & 0x2050) != 0x10 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3),
                    "CreateInstanceForAnotherGenericParameter")) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (first < 0 || owner < 1 ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "RuntimeTypeHandle") ||
                !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 2, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x1c)) return false;
            int template = SignatureReader.ReadClass(image, cursor, end);
            return RuntimeTypeMetadata.IsRuntimeTypeDefinition(image, heaps, layout, template) &&
                SignatureReader.ReadClass(image, cursor, end) == template && cursor[0] == end;
        }

        internal static int PublicDefaultConstructor(byte[] image, int[] layout, int[] heaps, int type)
        {
            var range = new int[2];
            if (ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return -1;
            var cursor = new int[1];
            for (int row = range[0]; row < range[1]; row++)
            {
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                if ((flags & 0x2017) != 6 || !ObjectLayouts.IsConstructor(image, heaps, layout, row)) continue;
                int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 0, cursor);
                if (end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) && cursor[0] == end)
                    return row;
            }
            return 0;
        }
    }
}
