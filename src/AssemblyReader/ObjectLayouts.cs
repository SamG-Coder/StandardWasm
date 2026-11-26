using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ObjectLayouts
    {
        internal const byte ObjectReference = 12, EventArgsReference = 13, EventHandlerReference = 14;
        internal const int FirstClass = ArrayTypeRefs.Doubles + 1, MaxClasses = 256 - FirstClass;
        private const int Header = 2, Width = 5, Assembly = 0, TypeRow = 1, FirstField = 2, Fields = 3, Storage = 4;
        internal const int FieldMapStart = Header + MaxClasses * Width;

        // The header stores class count and occupied field slots. Each class
        // maps an assembly-local TypeDef and Field range to its own field storage.
        internal static int Find(int[] registry, int assembly, int row)
        {
            for (int i = 0; i < registry[0]; i++)
            {
                int candidate = Slot(FirstClass + i);
                if (registry[candidate + Assembly] == assembly && registry[candidate + TypeRow] == row)
                    return FirstClass + i;
            }
            return 0;
        }

        internal static int Reserve(byte[] image, int[] streams, int[] methods, int[] registry, int assembly, int row, int[] shape)
        {
            if (registry[0] >= MaxClasses) return -2;

            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (start < 0 || row < 1 || row > counts[2]) return -1;
            int strings = methods[2];
            int refs = TypeRefStart(image, streams, counts, start, strings);
            int refWidth = ResolutionWidth(counts) + 2 * strings;
            int defs = refs + counts[1] * refWidth;
            int extends = CodedWidth(counts[2], counts[1], counts[27]);
            int fieldIndex = IndexWidth(counts[4]), methodIndex = IndexWidth(counts[6]);
            int fieldColumn = 4 + 2 * strings + extends;
            int defWidth = fieldColumn + fieldIndex + methodIndex;
            uint def = (uint)(defs + (row - 1) * defWidth);
            uint flags = U32(image, def);
            if ((flags & 0x120) != 0x100) return -2; // Sealed class, no interface.
            uint baseType = ReadIndex(image, def + (uint)(4 + 2 * strings), extends);
            if ((baseType & 3) != 1 || !IsObjectRef(image, streams, counts, refs, strings, (int)(baseType >> 2)))
                return -2;

            uint first = ReadIndex(image, def + (uint)fieldColumn, fieldIndex);
            uint end = row == counts[2] ? (uint)(counts[4] + 1) :
                ReadIndex(image, def + (uint)(defWidth + fieldColumn), fieldIndex);
            if (first < 1 || end < first || end > (uint)(counts[4] + 1)) return -1;
            int count = (int)(end - first), storage = FieldMapStart + registry[1];
            if (count > registry.Length - storage) return -3;
            // Reserve the class and its whole field slice before following
            // recursive references. Their registration may append other slices.
            int index = registry[0];
            registry[0] = index + 1;
            registry[1] = registry[1] + count;
            int slot = Slot(FirstClass + index);
            registry[slot + Assembly] = assembly;
            registry[slot + TypeRow] = row;
            registry[slot + FirstField] = (int)first;
            registry[slot + Fields] = count;
            registry[slot + Storage] = storage;
            shape[0] = defs + counts[2] * defWidth;
            shape[1] = (int)first;
            shape[2] = count;
            return FirstClass + index;
        }

        // Return storage directly for known types. A class signature yields
        // its image-local TypeDefOrRef code for the linker to resolve.
        internal static int ReadStorage(byte[] image, int[] streams, int[] methods, int table, int row, int[] reference)
        {
            reference[0] = 0;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (2 + strings + blobs));
            uint flags = U16(image, at);
            if ((flags & 0x50) == 0x50) return 0; // A literal has no instance storage.
            if ((flags & 0x10) != 0) return -2;
            uint sig = ReadIndex(image, at + (uint)(2 + strings), blobs);
            if (!Fits(sig, 3, (uint)streams[1])) return -1;
            uint blob = (uint)streams[0] + sig;
            int size = image[blob];
            if (size < 2 || !Fits(sig, (uint)(size + 1), (uint)streams[1])) return -1;
            if (image[blob + 1] != 6) return -2;
            byte element = image[blob + 2];
            if (size == 2 && element == 8) return 1;
            if (size == 2 && element == 0x0e) return 6;
            if (size == 2 && element == 0x1c) return ObjectReference;
            if (size == 2 && WideStorage.IsIntegerElement(element)) return WideStorage.Scalar;
            if (size == 3 && element == 0x1d)
            {
                byte type = ArrayTypeRefs.Storage(image[blob + 3]);
                return type == 0 ? -2 : type;
            }
            if (element != 0x12) return -2;
            int end = (int)blob + 1 + size;
            int coded = CompressedUnsigned(image, (int)blob + 3, end, reference);
            if (coded <= 0 || reference[0] != end) return -2;
            reference[0] = coded;
            return 0;
        }

        internal static int Owner(byte[] image, int[] streams, int[] methods, int member, bool field)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            int limit = field ? counts[4] : counts[6];
            if (start < 0 || member < 1 || member > limit) return -1;
            int strings = methods[2];
            int refs = TypeRefStart(image, streams, counts, start, strings);
            int defs = refs + counts[1] * (ResolutionWidth(counts) + 2 * strings);
            int fieldIndex = IndexWidth(counts[4]), methodIndex = IndexWidth(counts[6]);
            int column = 4 + 2 * strings + CodedWidth(counts[2], counts[1], counts[27]);
            int width = column + fieldIndex + methodIndex;
            if (!field) column += fieldIndex;
            int indexWidth = field ? fieldIndex : methodIndex;

            int owner = -1;
            uint previous = 1;
            for (int i = 0; i < counts[2]; i++)
            {
                uint index = ReadIndex(image, (uint)(defs + i * width + column), indexWidth);
                if (index < previous || index > (uint)(limit + 1)) return -1;
                if (index <= (uint)member) owner = i + 1;
                previous = index;
            }
            return owner;
        }

        private static int Slot(int type) => Header + (type - FirstClass) * Width;

        internal static int FieldCount(int[] registry, int type) => registry[Slot(type) + Fields];

        internal static int FieldTypeAt(int[] registry, int type, int offset) =>
            registry[registry[Slot(type) + Storage] + offset];

        internal static void SetFieldType(int[] registry, int type, int offset, int fieldType) =>
            registry[registry[Slot(type) + Storage] + offset] = fieldType;

        internal static int FieldType(int[] registry, int type, int row) =>
            FieldTypeAt(registry, type, row - registry[Slot(type) + FirstField]);

        internal static int FieldOrdinal(int[] registry, int type, int field)
        {
            int slot = Slot(type);
            int index = field - registry[slot + FirstField], storage = registry[slot + Storage];
            if (index < 0 || index >= registry[slot + Fields] || registry[storage + index] == 0)
                return -1;
            int ordinal = 0;
            for (int i = 0; i < index; i++)
                if (registry[storage + i] != 0) ordinal++;
            return ordinal;
        }

        internal static bool IsConstructor(byte[] image, int[] streams, int[] methods, int row)
        {
            int name = MethodDefinitions.ReadColumn(image, methods, row, 3);
            if (name <= 0 || (uint)name > (uint)streams[3] || !Fits((uint)name, 6, (uint)streams[3]))
                return false;
            uint at = (uint)(streams[2] + name);
            return U32(image, at) == 0x6f74632e && U16(image, at + 4) == 0x72;
        }

        internal static bool IsObjectConstructor(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (start < 0 || row < 1 || row > counts[10]) return false;
            int strings = methods[2], blobs = methods[3];
            int cursor = methods[0] + counts[6] * methods[1];
            int limit = streams[4] + streams[5];
            for (int table = 8; table <= 10; table++)
            {
                int width = StandaloneSignatures.Width(counts, table, strings, blobs);
                int skip = table == 10 ? row : counts[table];
                if (skip > (limit - cursor) / width) return false;
                cursor += skip * width;
            }
            int memberWidth = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(cursor - memberWidth);
            int parentWidth = memberWidth - strings - blobs;
            uint parent = ReadIndex(image, at, parentWidth);
            if ((parent & 7) != 1 ||
                !IsObjectRef(image, streams, counts, TypeRefStart(image, streams, counts, start, strings),
                    strings, (int)(parent >> 3)))
                return false;

            uint name = ReadIndex(image, at + (uint)parentWidth, strings);
            uint sig = ReadIndex(image, at + (uint)(parentWidth + strings), blobs);
            if (!Fits(name, 6, (uint)streams[3]) || !Fits(sig, 4, (uint)streams[1])) return false;
            uint nameAt = (uint)streams[2] + name, blob = (uint)streams[0] + sig;
            return U32(image, nameAt) == 0x6f74632e && U16(image, nameAt + 4) == 0x72 &&
                U32(image, blob) == 0x01002003; // .ctor, HASTHIS, zero args, void
        }

        private static bool IsObjectRef(byte[] image, int[] streams, int[] counts, int refs, int strings, int row)
        {
            if (row < 1 || row > counts[1]) return false;
            int scopeWidth = ResolutionWidth(counts);
            uint start = (uint)(refs + (row - 1) * (scopeWidth + 2 * strings));
            uint scope = ReadIndex(image, start, scopeWidth);
            if ((scope & 3) != 2 || scope >> 2 == 0 || scope >> 2 > (uint)counts[35]) return false;
            uint name = ReadIndex(image, start + (uint)scopeWidth, strings);
            uint ns = ReadIndex(image, start + (uint)(scopeWidth + strings), strings);
            if (!Fits(name, 7, (uint)streams[3]) || !Fits(ns, 7, (uint)streams[3])) return false;
            uint n = (uint)streams[2] + name, s = (uint)streams[2] + ns;
            return U32(image, n) == 0x656a624f && U16(image, n + 4) == 0x7463 && image[n + 6] == 0 &&
                U32(image, s) == 0x74737953 && U16(image, s + 4) == 0x6d65 && image[s + 6] == 0;
        }

        private static int TypeRefStart(byte[] image, int[] streams, int[] counts, int first, int strings) =>
            first + counts[0] * (2 + strings + 3 * ((image[streams[4] + 6] & 2) == 0 ? 2 : 4));

        private static int ResolutionWidth(int[] counts) =>
            CodedWidth(counts[0], counts[26], counts[35]) == 4 || counts[1] >= 16384 ? 4 : 2;

        private static int CodedWidth(int a, int b, int c) => a < 16384 && b < 16384 && c < 16384 ? 2 : 4;
        private static int IndexWidth(int count) => count < 65536 ? 2 : 4;

        private static uint ReadIndex(byte[] image, uint offset, int width) =>
            width == 2 ? U16(image, offset) : U32(image, offset);
    }
}
