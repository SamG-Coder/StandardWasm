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
            Measure(bundle, owner, type, result, depth, 0, null, false);

        internal static int MeasureFrame(BundleImages bundle, int owner, int type, int[] result) =>
            Measure(bundle, owner, type, result, 0, 0, null, true);

        // field[0] = byte offset, field[1] = ldind opcode for its element (0 for a nested struct).
        internal static int Field(BundleImages bundle, int owner, int type, int row, int[] field, bool scopedFrame = false)
        {
            var measured = new int[2];
            field[0] = -1;
            int status = Measure(bundle, owner, type, measured, 0, row, field, scopedFrame);
            if (status < 0) return status;
            return field[0] < 0 ? -1 : 0;
        }

        // Decision 0105: 1 when `type` is a measurable explicit value whose instance fields occupy
        // pairwise disjoint byte ranges, 0 when it is not, or a negative diagnostic. Disjoint fields
        // never alias, so the managed registry can keep them in independent slots.
        internal static int IsDisjointExplicit(BundleImages bundle, int owner, int type)
        {
            var measured = new int[2];
            var disjoint = new int[2];
            int status = Measure(bundle, owner, type, measured, 0, DisjointQuery, disjoint, false);
            return status < 0 ? status : disjoint[0];
        }

        private const int DisjointQuery = -1;

        private static int Measure(BundleImages bundle, int owner, int type, int[] result, int depth, int target, int[] found, bool scopedFrame)
        {
            if (depth > MaxDepth) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            int metadata = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (metadata < 0 || type < 1 || type > counts[2]) return -1;
            // Only the address-taking frame path can opt into a ref struct.
            // Every field must still pass the native layout check below.
            int inlineLength = ObjectLayouts.CheckValueAttributes(image, heaps, layout, type, scopedFrame);
            if (scopedFrame && inlineLength == ValueTypeAttributes.ByRefLike) inlineLength = 0;
            if (inlineLength < 0) return inlineLength;
            // [StructLayout(Pack = n)] and Size change the CLR's byte layout (Codex review of
            // #294: Pack=1 {byte, int} is 5 on the x86 CLR). They stay diagnosed.
            int packing = ObjectLayouts.CheckDefaultPacking(image, heaps, layout, type);
            if (packing < 0 && packing != -2) return packing;

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
            if (target == DisjointQuery && layoutKind != 0x10) return 0;
            if (layoutKind == 0x10)
                return Explicit(bundle, image, layout, heaps, owner, type, table, first, end, inlineLength, depth,
                    target, found, result);
            if (packing == -2)
                return FixedBuffer(image, layout, heaps, counts, metadata, type, layoutKind, table, first, end,
                    inlineLength, target, found, result);
            if (layoutKind == 0)
            {
                // Auto layout has no CLR-defined order, except for an enum: one value__ field.
                int storage = EnumStorage.Read(image, heaps, layout, table, first, end);
                if (storage < 0) return -2;
                int bytes = storage == SignatureTypes.I64 ? 8 : storage == SignatureTypes.I32 ? 4 :
                    storage == SignatureTypes.Signed16 || storage == SignatureTypes.Unsigned16 ? 2 : 1;
                result[0] = bytes;
                result[1] = bytes;
                // An enum field is a scalar in memory and in the managed registry: report
                // its underlying ldind so callers load it directly (Codex review of #296).
                if (result.Length > 2)
                    result[2] = storage == SignatureTypes.I64 ? 0x4c : storage == SignatureTypes.I32 ? 0x4a :
                        storage == SignatureTypes.Signed16 ? 0x48 : storage == SignatureTypes.Unsigned16 ? 0x49 :
                        storage == SignatureTypes.Signed8 ? 0x46 : 0x47;
                return 0;
            }
            // Sequential layout (0x08). Explicit layout (0x10) needs its FieldLayout offsets.
            if (layoutKind != 0x08) return -2;
            var field = new int[3];
            int offset = 0, align = 1;
            var nested = new int[3];
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
                    nested[2] = 0; // only an enum reports an underlying load
                    status = Measure(bundle, identity[0], identity[1], nested, depth + 1);
                    if (status < 0) return status;
                    size = nested[0];
                    int fieldAlign = nested[1];
                    offset = (offset + fieldAlign - 1) / fieldAlign * fieldAlign;
                    // A nested struct is loaded field by field (0); an enum loads as its underlying scalar.
                    if (row == target) { found[0] = offset; found[1] = nested[2]; }
                    offset += size;
                    if (fieldAlign > align) align = fieldAlign;
                    continue;
                }
                size = PrimitiveSize(element);
                if (size < 0) return -2; // references and other non-blittable fields
                offset = (offset + size - 1) / size * size;
                if (row == target) { found[0] = offset; found[1] = LoadOpcode(element); }
                offset += size;
                if (size > align) align = size;
            }
            if (offset == 0) { result[0] = 1; result[1] = 1; return 0; }
            // The CLR lays out n elements of the one declared field back to back.
            if (inlineLength > 0) offset = (offset + align - 1) / align * align * inlineLength;
            result[0] = (offset + align - 1) / align * align;
            result[1] = align;
            return 0;
        }

        // Decision 0105: a blittable [StructLayout(LayoutKind.Explicit, Size = n)] value. Each instance
        // field sits at its FieldLayout offset; fields may overlap, as only primitives and blittable
        // values (fixed buffers included) are accepted. sizeof is the declared Size when it covers
        // every field and is a multiple of the alignment, which is the CLR's result; any other
        // declared size, and an explicit layout without one, stays diagnosed.
        private static int Explicit(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int type,
            int table, int first, int end, int inlineLength, int depth, int target, int[] found, int[] result)
        {
            if (inlineLength != 0) return -2;
            var offsets = new int[end - first];
            int extent = ExplicitValueLayouts.ReadNative(image, heaps, layout, type, first, offsets);
            if (extent < 0) return extent;
            var field = new int[3];
            var nested = new int[3];
            var starts = new int[end - first];
            var sizes = new int[end - first];
            int reach = 0, align = 1;
            for (int row = first; row < end; row++)
            {
                AssemblyReferences.ReadField(image, layout, table, row, field);
                if ((field[0] & 0x10) != 0) continue; // static
                int offset = offsets[row - first] - 1;
                if (offset < 0) return -1; // an explicit instance field needs its FieldLayout row
                int blob = field[2];
                if (blob <= 0 || blob >= heaps[1]) return -1;
                int limit = heaps[0] + heaps[1];
                var cursor = new int[1];
                int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
                int at = cursor[0];
                if (length < 2 || image[at] != 0x06) return -1;
                int element = image[at + 1];
                int size, fieldAlign, load;
                if (element == 0x11)
                {
                    int coded = CompressedUnsigned(image, at + 2, at + length, cursor);
                    var identity = new int[2];
                    int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
                    if (status < 0) return status;
                    nested[2] = 0;
                    status = Measure(bundle, identity[0], identity[1], nested, depth + 1);
                    if (status < 0) return status;
                    size = nested[0];
                    fieldAlign = nested[1];
                    load = nested[2];
                }
                else
                {
                    size = PrimitiveSize(element);
                    if (size < 0) return -2; // references and other non-blittable fields
                    fieldAlign = size;
                    load = LoadOpcode(element);
                }
                if (offset % fieldAlign != 0) return -2; // a misaligned field changes the CLR's packing rules
                if (row == target) { found[0] = offset; found[1] = load; }
                starts[row - first] = offset;
                sizes[row - first] = size;
                if (offset + size > reach) reach = offset + size;
                if (fieldAlign > align) align = fieldAlign;
            }
            if (extent == 0 || extent < reach || extent % align != 0) return -2;
            if (target == DisjointQuery)
            {
                found[0] = 1;
                for (int a = 0; a < starts.Length; a++)
                    for (int b = a + 1; b < starts.Length; b++)
                        if (sizes[a] != 0 && sizes[b] != 0 &&
                            starts[a] < starts[b] + sizes[b] && starts[b] < starts[a] + sizes[a]) found[0] = 0;
            }
            result[0] = extent;
            result[1] = align;
            return 0;
        }

        private static int FixedBuffer(byte[] image, int[] layout, int[] heaps, int[] counts,
            int metadata, int type, uint layoutKind, int table, int first, int end, int inlineLength,
            int target, int[] found, int[] result)
        {
            if (layoutKind != 0x08 || inlineLength != 0 || end - first != 1) return -2;
            var element = new int[2];
            int extent = FixedBufferMetadata.Read(image, layout, heaps, counts, metadata, type, table, first, element);
            if (extent < 0) return extent;
            result[0] = extent;
            result[1] = element[1];
            if (target == first)
            {
                found[0] = 0;
                found[1] = LoadOpcode(element[0]);
            }
            return 0;
        }

        // ldind.* for a primitive field: i1, u1, i2, u2, i4, u4, i8, r4, r8, i.
        private static int LoadOpcode(int element) =>
            element == 4 ? 0x46 : element == 2 || element == 5 ? 0x47 : element == 6 ? 0x48 :
            element == 3 || element == 7 ? 0x49 : element == 8 ? 0x4a : element == 9 ? 0x4b :
            element == 0x0a || element == 0x0b ? 0x4c : element == 0x0c ? 0x4e : element == 0x0d ? 0x4f : 0x4d;

        internal static int PrimitiveSize(int element) =>
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
