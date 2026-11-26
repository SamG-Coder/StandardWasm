using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class TypeInitializers
    {
        internal static int RejectModuleInitializer(byte[] image, int[] layout, int[] heaps) =>
            RejectDeclaringType(image, layout, heaps, 1); // TypeDef 1 is the CLI <Module> type.

        internal static int RejectDeclaringType(byte[] image, int[] layout, int[] heaps, int type)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                type < 1 || type > counts[2]) return -1;
            int strings = layout[2];
            int fields = counts[4] < 65536 ? 2 : 4;
            int methods = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int methodColumn = 4 + 2 * strings + extends + fields;
            int rowWidth = methodColumn + methods;
            // CompressedTables already validated the TypeDef and Field tables.
            int table = layout[0] - counts[4] * (2 + strings + layout[3]) - counts[2] * rowWidth;
            uint at = (uint)(table + (type - 1) * rowWidth + methodColumn);
            int first = MethodIndex(image, at, methods);
            int end = type == counts[2] ? counts[6] + 1 : MethodIndex(image, at + (uint)rowWidth, methods);
            if (first < 1 || end < first || end > counts[6] + 1) return -1;

            // Keep conservative semantics for both precise and beforefieldinit
            // types. No reached initializer is skipped until we can execute it.
            for (int row = first; row < end; row++)
                if (MethodNames.IsInitializer(image, layout, heaps, row)) return -2;
            return 0;
        }

        private static int MethodIndex(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
