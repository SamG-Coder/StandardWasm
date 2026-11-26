using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ValueTypeAttributes
    {
        // Ordinary values return zero. Layout-changing attributes must either
        // supply a valid element count or stop aggregate registration.
        internal static int ReadInlineArrayLength(byte[] image, int[] heaps, int[] methods, int[] counts, int typeRow,
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
            int inlineLength = 0;
            for (int index = 0; index < counts[12]; index++)
            {
                uint at = (uint)(table + index * width);
                if (AttributeConstructors.ReadRowIndex(image, at, parentWidth) != parent) continue;
                int constructor = AttributeConstructors.ReadRowIndex(image, at + (uint)parentWidth, constructorWidth);
                int type = AttributeConstructors.ReadType(image, heaps, methods, counts, constructor);
                if (type < 0) return type;
                if (type == 0) continue;

                int row = type >> 3, tag = type & 7;
                int start = tag == 1 ? refs : defs;
                int rowWidth = tag == 1 ? refWidth : defWidth;
                int nameColumn = tag == 1 ? refWidth - 2 * strings : 4;
                uint nameAt = (uint)(start + (row - 1) * rowWidth + nameColumn);
                int name = AttributeConstructors.ReadRowIndex(image, nameAt, strings);
                int space = AttributeConstructors.ReadRowIndex(image, nameAt + (uint)strings, strings);
                int shape = ShapeAttribute(image, heaps, name, space);
                if (shape == 0) continue;
                if (shape == 1) return -2; // Ref-like values need lifetime analysis.
                if (inlineLength != 0) return -1;
                int payload = AttributeConstructors.ReadRowIndex(image, at + (uint)(parentWidth + constructorWidth), blobs);
                inlineLength = ReadInlineLength(image, heaps, methods, counts, constructor, type, payload);
                if (inlineLength < 0) return inlineLength;
            }
            return inlineLength;
        }

        private static int ShapeAttribute(byte[] image, int[] heaps, int name, int space)
        {
            uint size = (uint)heaps[3];
            if (!Fits((uint)name, 21, size) || !Fits((uint)space, 32, size)) return 0;
            uint n = (uint)(heaps[2] + name), s = (uint)(heaps[2] + space);
            if (U32(image, s) != 0x74737953 || U32(image, s + 4) != 0x522e6d65 ||
                U32(image, s + 8) != 0x69746e75 || U32(image, s + 12) != 0x432e656d ||
                U32(image, s + 16) != 0x69706d6f || U32(image, s + 20) != 0x5372656c ||
                U32(image, s + 24) != 0x69767265 || U32(image, s + 28) != 0x00736563 ||
                image[n + 20] != 0 || U32(image, n + 12) != 0x69727474 ||
                U32(image, n + 16) != 0x65747562) return 0;

            if (U32(image, n) == 0x79427349 && U32(image, n + 4) == 0x4c666552 &&
                U32(image, n + 8) == 0x41656b69) return 1; // IsByRefLikeAttribute
            return U32(image, n) == 0x696c6e49 && U32(image, n + 4) == 0x7241656e &&
                U32(image, n + 8) == 0x41796172 ? 2 : 0; // InlineArrayAttribute
        }

        private static int ReadInlineLength(byte[] image, int[] heaps, int[] methods, int[] counts,
            int constructor, int type, int payload)
        {
            int name, signature;
            int row = constructor >> 3;
            if ((constructor & 7) == 2)
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return -2;
                name = MethodDefinitions.ReadColumn(image, methods, row, 3);
                signature = MethodDefinitions.ReadColumn(image, methods, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if ((type & 7) != 1 || AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                    reference[AssemblyReferences.Depth] != 0 ||
                    !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts,
                        reference[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false)) return -2;
                name = reference[4];
                signature = reference[5];
            }
            if (name < 1 || !Fits((uint)name, 6, (uint)heaps[3])) return -1;
            uint nameAt = (uint)(heaps[2] + name);
            if (U32(image, nameAt) != 0x6f74632e || U16(image, nameAt + 4) != 0x72) return -2;
            if (signature < 1 || signature >= heaps[1] || payload < 1 || payload >= heaps[1]) return -1;
            var next = new int[1];
            int end = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, end, next);
            if (size != 4 || size > end - next[0] || U32(image, (uint)next[0]) != 0x08010120) return -2;
            size = CompressedUnsigned(image, heaps[0] + payload, end, next);
            if (size != 8 || size > end - next[0]) return -1;
            uint at = (uint)next[0];
            if (U16(image, at) != 1 || U16(image, at + 6) != 0) return -1;
            int length = unchecked((int)U32(image, at + 2));
            return length > 0 ? length : -1;
        }
    }
}
