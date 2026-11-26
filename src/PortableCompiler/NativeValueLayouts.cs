using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Stage 143 (decision 0071). The CLR's 32-bit `sizeof` of a sequential, blittable value type:
    // natural alignment (8-byte fields align to 8 even on x86), the size rounded up to the
    // largest alignment, and an empty struct of size 1. Read from field signatures, because
    // the value-type registry stores bool as I32.
    internal static class NativeValueLayouts
    {
        private const int MaxDepth = 16;

        // result[0] = size, result[1] = alignment. Returns 0, or a negative diagnostic.
        internal static int Measure(BundleImages bundle, int owner, int type, int[] result, int depth = 0) =>
            Measure(bundle, owner, type, result, depth, 0, null);

        // field[0] = byte offset, field[1] = ldind opcode for its element (0 for a nested struct).
        internal static int Field(BundleImages bundle, int owner, int type, int row, int[] field)
        {
            var measured = new int[2];
            field[0] = -1;
            int status = Measure(bundle, owner, type, measured, 0, row, field);
            if (status < 0) return status;
            return field[0] < 0 ? -1 : 0;
        }

        private static int Measure(BundleImages bundle, int owner, int type, int[] result, int depth, int target, int[] found)
        {
            if (depth > MaxDepth) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || type < 1 || type > counts[2]) return -1;
            // [InlineArray] repeats its field and ref structs hold byrefs: neither is
            // measured from its declared field rows (decision 0071).
            int attributes = ObjectLayouts.CheckValueAttributes(image, heaps, layout, type);
            if (attributes < 0) return attributes;

            int strings = layout[2];
            int fieldIndex = counts[4] < 65536 ? 2 : 4;
            int methodIndex = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int fieldColumn = 4 + 2 * strings + extends;
            int rowWidth = fieldColumn + fieldIndex + methodIndex;
            uint definition = (uint)TypeDefinition(layout, counts, type, rowWidth);
            int first = Index(image, definition + (uint)fieldColumn, fieldIndex);
            int end = type == counts[2] ? counts[4] + 1 : Index(image, definition + (uint)(fieldColumn + rowWidth), fieldIndex);
            if (first < 1 || end < first || end > counts[4] + 1) return -1;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return table;
            uint layoutKind = U32(image, definition) & 0x18;
            if (layoutKind == 0)
            {
                // Auto layout has no CLR-defined order, except for an enum: one value__ field.
                int storage = EnumStorage.Read(image, heaps, layout, table, first, end);
                if (storage < 0) return -2;
                int bytes = storage == SignatureTypes.I64 ? 8 : storage == SignatureTypes.I32 ? 4 :
                    storage == SignatureTypes.Signed16 || storage == SignatureTypes.Unsigned16 ? 2 : 1;
                result[0] = bytes;
                result[1] = bytes;
                return 0;
            }
            // Sequential layout (0x08). Explicit layout (0x10) needs its FieldLayout offsets.
            if (layoutKind != 0x08) return -2;
            var field = new int[3];
            int offset = 0, align = 1;
            var nested = new int[2];
            for (int row = first; row < end; row++)
            {
                AssemblyReferences.ReadField(image, layout, table, row, field);
                if ((field[0] & 0x10) != 0) continue; // static
                int blob = field[2];
                if (blob <= 0 || blob >= heaps[1]) return -1;
                int limit = heaps[0] + heaps[1];
                var cursor = new int[1];
                int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
                int at = cursor[0];
                if (length < 2 || image[at] != 0x06) return -1;
                int element = image[at + 1];
                int size;
                if (element == 0x11)
                {
                    int coded = CompressedUnsigned(image, at + 2, at + length, cursor);
                    var identity = new int[2];
                    int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
                    if (status < 0) return status;
                    status = Measure(bundle, identity[0], identity[1], nested, depth + 1);
                    if (status < 0) return status;
                    size = nested[0];
                    int fieldAlign = nested[1];
                    offset = (offset + fieldAlign - 1) / fieldAlign * fieldAlign;
                    if (row == target) { found[0] = offset; found[1] = 0; }
                    offset += size;
                    if (fieldAlign > align) align = fieldAlign;
                    continue;
                }
                size = Primitive(element);
                if (size < 0) return -2; // references and other non-blittable fields
                offset = (offset + size - 1) / size * size;
                if (row == target) { found[0] = offset; found[1] = LoadOpcode(element); }
                offset += size;
                if (size > align) align = size;
            }
            if (offset == 0) { result[0] = 1; result[1] = 1; return 0; }
            result[0] = (offset + align - 1) / align * align;
            result[1] = align;
            return 0;
        }

        // ldind.* for a primitive field: i1, u1, i2, u2, i4, u4, i8, r4, r8, i.
        private static int LoadOpcode(int element) =>
            element == 4 ? 0x46 : element == 2 || element == 5 ? 0x47 : element == 6 ? 0x48 :
            element == 3 || element == 7 ? 0x49 : element == 8 ? 0x4a : element == 9 ? 0x4b :
            element == 0x0a || element == 0x0b ? 0x4c : element == 0x0c ? 0x4e : element == 0x0d ? 0x4f : 0x4d;

        private static int Primitive(int element) =>
            element == 2 || element == 4 || element == 5 ? 1 :           // bool, i1, u1
            element == 3 || element == 6 || element == 7 ? 2 :           // char, i2, u2
            element == 8 || element == 9 || element == 0x0c ? 4 :        // i4, u4, r4
            element == 0x0a || element == 0x0b || element == 0x0d ? 8 :  // i8, u8, r8
            element == 0x18 || element == 0x19 || element == 0x0f ? 4 :  // I, U, pointer (wasm32)
            -1;

        private static int TypeDefinition(int[] layout, int[] counts, int type, int rowWidth) =>
            layout[0] - counts[4] * (2 + layout[2] + layout[3]) - (counts[2] - type + 1) * rowWidth;

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
