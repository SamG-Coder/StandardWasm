using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ArrayInitializerMetadata
    {
        private const int Helpers = 0, Array = 1, FieldHandle = 2, ValueType = 3;

        internal static int FindData(byte[] image, int[] heaps, int[] methods, int member, int field, int byteCount)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || member < 1 || member > counts[10] || field < 1 || field > counts[4])
                return -1;
            if (!IsInitializeArray(image, heaps, methods, counts, first, member))
                return -2;

            return FindStaticData(image, heaps, methods, counts, first, field, byteCount);
        }

        internal static int FindStaticData(byte[] image, int[] heaps, int[] methods, int field, int byteCount)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || field < 1 || field > counts[4] || byteCount < 0) return -1;
            return FindStaticData(image, heaps, methods, counts, first, field, byteCount);
        }

        private static int FindStaticData(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int field, int byteCount)
        {
            int strings = methods[2], blobs = methods[3];
            int fieldWidth = 2 + strings + blobs;
            uint at = (uint)(methods[0] - (counts[4] - field + 1) * fieldWidth);
            if ((U16(image, at) & 0x130) != 0x130) return -2; // Static, InitOnly and HasFieldRVA.
            int signature = Index(image, at + (uint)(2 + strings), blobs);
            int size = FieldSize(image, heaps, methods, counts, first, signature);
            if (size < byteCount) return -2;

            int table = MetadataTableRows.TableStart(heaps, methods, counts, 29);
            if (table < 0) return -1;
            int indexWidth = IndexWidth(counts[4]);
            uint rva = 0;
            for (int row = 0; row < counts[29]; row++)
            {
                uint entry = (uint)(table + row * (4 + indexWidth));
                if (Index(image, entry + 4, indexWidth) != field) continue;
                if (rva != 0) return -1;
                rva = U32(image, entry);
                if (rva == 0) return -1;
            }
            return rva == 0 ? -1 : PeLocator.MapImageRange(image, rva, (uint)size);
        }

        private static bool IsInitializeArray(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int row)
        {
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 10);
            if (table < 0) return false;
            int strings = methods[2], blobs = methods[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(table + (row - 1) * width);
            int parentWidth = width - strings - blobs;
            int parent = Index(image, at, parentWidth);
            if ((parent & 7) != 1 || !IsSystemType(image, heaps, methods, counts, first, parent >> 3, Helpers))
                return false;
            int name = Index(image, at + (uint)parentWidth, strings);
            if (!Fits((uint)name, 16, (uint)heaps[3])) return false;
            uint n = (uint)(heaps[2] + name);
            if (U32(image, n) != 0x74696e49 || U32(image, n + 4) != 0x696c6169 ||
                U32(image, n + 8) != 0x7241657a || U32(image, n + 12) != 0x00796172)
                return false; // InitializeArray

            int signature = Index(image, at + (uint)(parentWidth + strings), blobs);
            var next = new int[1];
            int size = Blob(image, heaps, signature, next);
            int start = next[0];
            if (size < 7 || U32(image, (uint)start) != 0x12010200) return false; // static void (class ...)
            int end = start + size;
            int array = CompressedUnsigned(image, start + 4, end, next);
            if ((array & 3) != 1 || !IsSystemType(image, heaps, methods, counts, first, array >> 2, Array) ||
                next[0] >= end || image[next[0]] != 0x11) return false;
            int handle = CompressedUnsigned(image, next[0] + 1, end, next);
            return (handle & 3) == 1 && next[0] == end &&
                IsSystemType(image, heaps, methods, counts, first, handle >> 2, FieldHandle);
        }

        private static int FieldSize(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int signature)
        {
            var next = new int[1];
            int size = Blob(image, heaps, signature, next);
            int start = next[0];
            if (size < 2 || image[start] != 6) return -1;
            int element = image[start + 1];
            if (size == 2)
            {
                if (element >= 4 && element <= 5) return 1;
                if (element >= 6 && element <= 7) return 2;
                if (element == 8 || element == 9) return 4;
                if (element == 10 || element == 11) return 8;
                return -1;
            }
            if (element != 0x11) return -1;
            int type = CompressedUnsigned(image, start + 2, start + size, next);
            int row = type >> 2;
            if ((type & 3) != 0 || row < 1 || row > counts[2] || next[0] != start + size) return -1;

            int strings = methods[2], codedWidth = TypeWidth(counts);
            int fieldWidth = IndexWidth(counts[4]);
            int defFields = 4 + 2 * strings + codedWidth;
            int defWidth = defFields + fieldWidth + IndexWidth(counts[6]);
            uint def = (uint)(TypeRefs(image, heaps, methods, first, counts[0]) + counts[1] * (ScopeWidth(counts) + 2 * strings) +
                (row - 1) * defWidth);
            int baseType = Index(image, def + (uint)(4 + 2 * strings), codedWidth);
            bool platformValue = (baseType & 3) == 1 ?
                IsSystemType(image, heaps, methods, counts, first, baseType >> 2, ValueType) :
                (baseType & 3) == 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts) &&
                ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, baseType >> 2, "System", "ValueType");
            if ((U32(image, def) & 0x18) != 0x10 || !platformValue) return -1;
            int fieldStart = Index(image, def + (uint)defFields, fieldWidth);
            int fieldEnd = row == counts[2] ? counts[4] + 1 : Index(image, def + (uint)(defWidth + defFields), fieldWidth);
            // Roslyn's explicit-layout holder has no instance fields or references.
            if (fieldStart < 1 || fieldStart > counts[4] + 1 || fieldStart != fieldEnd) return -1;

            int table = MetadataTableRows.TableStart(heaps, methods, counts, 15);
            if (table < 0) return -1;
            int parentWidth = IndexWidth(counts[2]);
            int declaredSize = -1;
            for (int i = 0; i < counts[15]; i++)
            {
                uint layout = (uint)(table + i * (6 + parentWidth));
                if (Index(image, layout + 6, parentWidth) != row) continue;
                uint bytes = U32(image, layout + 2);
                if (declaredSize >= 0 || bytes == 0 || bytes > int.MaxValue) return -1;
                declaredSize = (int)bytes;
            }
            return declaredSize;
        }

        private static bool IsSystemType(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int row, int kind)
        {
            if (row < 1 || row > counts[1]) return false;
            int strings = methods[2], width = ScopeWidth(counts);
            uint at = (uint)(TypeRefs(image, heaps, methods, first, counts[0]) + (row - 1) * (width + 2 * strings));
            int scope = Index(image, at, width);
            if ((scope & 3) != 2 || !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts, scope >> 2))
                return false;
            uint name = (uint)Index(image, at + (uint)width, strings);
            uint ns = (uint)Index(image, at + (uint)(width + strings), strings);
            uint heap = (uint)heaps[2], limit = (uint)heaps[3];
            uint n = heap + name, s = heap + ns;
            if (kind == Helpers)
            {
                // System.Runtime.CompilerServices.RuntimeHelpers
                return Fits(ns, 32, limit) && Fits(name, 15, limit) &&
                    U32(image, s) == 0x74737953 && U32(image, s + 4) == 0x522e6d65 &&
                    U32(image, s + 8) == 0x69746e75 && U32(image, s + 12) == 0x432e656d &&
                    U32(image, s + 16) == 0x69706d6f && U32(image, s + 20) == 0x5372656c &&
                    U32(image, s + 24) == 0x69767265 && U32(image, s + 28) == 0x00736563 &&
                    U32(image, n) == 0x746e7552 && U32(image, n + 4) == 0x48656d69 &&
                    U32(image, n + 8) == 0x65706c65 && U16(image, n + 12) == 0x7372 && image[n + 14] == 0;
            }
            if (!Fits(ns, 7, limit) || U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0)
                return false;
            if (kind == Array)
                return Fits(name, 6, limit) && U32(image, n) == 0x61727241 && U16(image, n + 4) == 0x79;
            if (kind == ValueType)
                return Fits(name, 10, limit) && U32(image, n) == 0x756c6156 && U32(image, n + 4) == 0x70795465 &&
                    U16(image, n + 8) == 0x65;
            return Fits(name, 19, limit) && U32(image, n) == 0x746e7552 && U32(image, n + 4) == 0x46656d69 &&
                U32(image, n + 8) == 0x646c6569 && U32(image, n + 12) == 0x646e6148 &&
                U16(image, n + 16) == 0x656c && image[n + 18] == 0;
        }

        private static int Blob(byte[] image, int[] heaps, int index, int[] next)
        {
            if (index <= 0 || index >= heaps[1]) return -1;
            int end = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + index, end, next);
            return length <= end - next[0] ? length : -1;
        }

        private static int TypeRefs(byte[] image, int[] heaps, int[] methods, int first, int modules) =>
            first + modules * (2 + methods[2] + 3 * ((image[heaps[4] + 6] & 2) == 0 ? 2 : 4));

        private static int ScopeWidth(int[] counts) =>
            counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;

        private static int TypeWidth(int[] counts) => counts[1] < 16384 && counts[2] < 16384 && counts[27] < 16384 ? 2 : 4;
        private static int IndexWidth(int count) => count < 65536 ? 2 : 4;
        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
