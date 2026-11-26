using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class TypeInitializers
    {
        internal static int RejectModuleInitializer(byte[] image, int[] layout, int[] heaps) =>
            RejectDeclaringType(image, layout, heaps, 1); // TypeDef 1 is the CLI <Module> type.

        internal static int RejectDeclaringType(byte[] image, int[] layout, int[] heaps, int type, bool allowBeforeFieldInit = false)
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
            uint definition = (uint)(table + (type - 1) * rowWidth);
            uint at = definition + (uint)methodColumn;
            int first = MethodIndex(image, at, methods);
            int end = type == counts[2] ? counts[6] + 1 : MethodIndex(image, at + (uint)rowWidth, methods);
            if (first < 1 || end < first || end > counts[6] + 1) return -1;

            // ECMA-335 I.8.9.5 permits BeforeFieldInit to wait for the first
            // static field access. A supported non-triggering call can
            // skip this guard. The linker still checks every static field.
            bool mayDefer = allowBeforeFieldInit && (U32(image, definition) & 0x00100000) != 0;
            bool found = false;
            for (int row = first; row < end; row++)
            {
                if (!MethodNames.IsInitializer(image, layout, heaps, row)) continue;
                if (!mayDefer || found || !HasInitializerShape(image, layout, heaps, row)) return -2;
                found = true;
            }
            return 0;
        }

        private static bool HasInitializerShape(byte[] image, int[] layout, int[] heaps, int row)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            if ((attributes & 0x1810) != 0x1810) return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            // Default static calling convention, zero parameters, void.
            return length == 3 && start <= limit - 3 &&
                image[start] == 0 && image[start + 1] == 0 && image[start + 2] == 1;
        }

        private static int MethodIndex(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
