using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ValueTypeAttributes
    {
        internal static int CheckSupported(byte[] image, int[] heaps, int[] methods, int[] counts, int typeRow,
            int refs, int refWidth, int defs, int defWidth)
        {
            if (counts[12] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 12);
            if (table < 0) return -1;
            int strings = methods[2], blobs = methods[3];
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int width = StandaloneSignatures.Width(counts, 12, strings, blobs);
            int constructorWidth = width - parentWidth - blobs;
            int parent = typeRow << 5 | 3; // HasCustomAttribute.TypeDef.
            for (int index = 0; index < counts[12]; index++)
            {
                uint at = (uint)(table + index * width);
                if (Index(image, at, parentWidth) != parent) continue;
                int constructor = Index(image, at + (uint)parentWidth, constructorWidth);
                int type = AttributeType(image, heaps, methods, counts, constructor);
                if (type < 0) return type;
                if (type == 0) continue;

                int row = type >> 3, tag = type & 7;
                int start = tag == 1 ? refs : defs;
                int rowWidth = tag == 1 ? refWidth : defWidth;
                int nameColumn = tag == 1 ? refWidth - 2 * strings : 4;
                uint nameAt = (uint)(start + (row - 1) * rowWidth + nameColumn);
                int name = Index(image, nameAt, strings);
                int space = Index(image, nameAt + (uint)strings, strings);
                // Ref-like values need lifetime rules. Inline arrays repeat
                // their declared field and cannot use the ordinary aggregate.
                if (ChangesValueShape(image, heaps, name, space)) return -2;
            }
            return 0;
        }

        private static int AttributeType(byte[] image, int[] heaps, int[] methods, int[] counts, int constructor)
        {
            int row = constructor >> 3, kind = constructor & 7;
            if (kind == 2)
            {
                int owner = ObjectLayouts.Owner(image, heaps, methods, row, false);
                return owner < 1 ? -1 : owner << 3;
            }
            if (kind != 3 || row < 1 || row > counts[10]) return -1;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 10);
            if (table < 0) return -1;
            int width = StandaloneSignatures.Width(counts, 10, methods[2], methods[3]);
            int parent = Index(image, (uint)(table + (row - 1) * width), width - methods[2] - methods[3]);
            int tag = parent & 7;
            if (tag > 1) return 0;
            int typeRow = parent >> 3;
            return typeRow > 0 && typeRow <= counts[tag == 1 ? 1 : 2] ? parent : -1;
        }

        private static bool ChangesValueShape(byte[] image, int[] heaps, int name, int space)
        {
            uint size = (uint)heaps[3];
            if (!Fits((uint)name, 21, size) || !Fits((uint)space, 32, size)) return false;
            uint n = (uint)(heaps[2] + name), s = (uint)(heaps[2] + space);
            if (U32(image, s) != 0x74737953 || U32(image, s + 4) != 0x522e6d65 ||
                U32(image, s + 8) != 0x69746e75 || U32(image, s + 12) != 0x432e656d ||
                U32(image, s + 16) != 0x69706d6f || U32(image, s + 20) != 0x5372656c ||
                U32(image, s + 24) != 0x69767265 || U32(image, s + 28) != 0x00736563 ||
                image[n + 20] != 0) return false;

            // IsByRefLikeAttribute and InlineArrayAttribute.
            return U32(image, n + 12) == 0x69727474 && U32(image, n + 16) == 0x65747562 &&
                (U32(image, n) == 0x79427349 && U32(image, n + 4) == 0x4c666552 &&
                    U32(image, n + 8) == 0x41656b69 ||
                U32(image, n) == 0x696c6e49 && U32(image, n + 4) == 0x7241656e &&
                    U32(image, n + 8) == 0x41796172);
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
