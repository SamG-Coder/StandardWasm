using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class AssemblyReferences
    {
        // Allocation TypeRefs must name a primitive in a platform assembly.
        // Validate the exact AssemblyRef name, token and neutral culture.
        internal static bool IsPlatformPrimitiveAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row)
        {
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 35);
            if (table < 0 || row < 1 || row > counts[35]) return false;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (12 + 2 * strings + 2 * blobs));
            if (U32(image, at + 8) != 0 || Index(image, at + (uint)(12 + blobs + strings), strings) != 0) return false;
            uint key = Index(image, at + 12, blobs);
            if (key == 0 || !Fits(key, 9, (uint)heaps[1])) return false;
            uint blob = (uint)heaps[0] + key;
            if (image[blob] != 8) return false;
            uint low = U32(image, blob + 1), high = U32(image, blob + 5);
            uint name = Index(image, at + (uint)(12 + blobs), strings);
            uint n = (uint)heaps[2] + name, size = (uint)heaps[3];
            if (name == 0) return false;
            if (low == 0xff137bcc && high == 0x51dd2dcd) // netstandard
                return Fits(name, 12, size) && U32(image, n) == 0x7374656e &&
                    U32(image, n + 4) == 0x646e6174 && U32(image, n + 8) == 0x647261;
            if (low == 0x565c7ab7 && high == 0x89e03419) // mscorlib
                return Fits(name, 9, size) && U32(image, n) == 0x6f63736d &&
                    U32(image, n + 4) == 0x62696c72 && image[n + 8] == 0;
            if (low == 0x7f5f3fb0 && high == 0x3a0ad511) // System.Runtime, System.Runtime.Extensions
            {
                if (!Fits(name, 15, size) || U32(image, n) != 0x74737953 ||
                    U32(image, n + 4) != 0x522e6d65 || U32(image, n + 8) != 0x69746e75)
                    return false;
                return U16(image, n + 12) == 0x656d && image[n + 14] == 0 ||
                    Fits(name, 26, size) && U32(image, n + 12) == 0x452e656d && U32(image, n + 16) == 0x6e657478 &&
                    U32(image, n + 20) == 0x6e6f6973 && U16(image, n + 24) == 0x73;
            }
            // System.Private.CoreLib
            return low == 0xd785ec7c && high == 0x8e79a7be && Fits(name, 23, size) &&
                U32(image, n) == 0x74737953 && U32(image, n + 4) == 0x502e6d65 &&
                U32(image, n + 8) == 0x61766972 && U32(image, n + 12) == 0x432e6574 &&
                U32(image, n + 16) == 0x4c65726f && U16(image, n + 20) == 0x6269 && image[n + 22] == 0;
        }

        // Version byte offset, assembly/type/namespace/member string indexes, signature blob index.
        internal static int ReadMember(byte[] image, int[] streams, int[] methods, int row, int[] reference)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return -1;
            int strings = methods[2], blobs = methods[3];
            int table = MetadataTableRows.TableStart(streams, methods, counts, 10);
            if (table < 0) return -1;
            int memberWidth = StandaloneSignatures.Width(counts, 10, strings, blobs);
            int parentWidth = memberWidth - strings - blobs;
            uint member = (uint)(table + (row - 1) * memberWidth);
            uint parent = Index(image, member, parentWidth);
            if ((parent & 7) != 1) return -2; // TypeRef only.
            int status = ReadTypeReference(image, streams, methods, counts, first, (int)(parent >> 3), reference);
            if (status < 0) return status;
            reference[4] = (int)Index(image, member + (uint)parentWidth, strings);
            reference[5] = (int)Index(image, member + (uint)(parentWidth + strings), blobs);
            return 0;
        }

        internal static int ReadType(byte[] image, int[] streams, int[] methods, int row, int[] reference)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0) return -1;
            return ReadTypeReference(image, streams, methods, counts, first, row, reference);
        }

        private static int ReadTypeReference(byte[] image, int[] streams, int[] methods, int[] counts,
            int first, int typeRow, int[] reference)
        {
            if (typeRow < 1 || typeRow > counts[1]) return -1;
            int strings = methods[2], blobs = methods[3];
            int scopeWidth = ScopeWidth(counts);
            int typeRefWidth = scopeWidth + 2 * strings;
            int typeRefs = TypeRefStart(image, streams, counts, first, strings);
            uint type = (uint)(typeRefs + (typeRow - 1) * typeRefWidth);
            uint scope = Index(image, type, scopeWidth);
            if ((scope & 3) != 2) return -2;
            int assemblyRow = (int)(scope >> 2);
            if (assemblyRow < 1 || assemblyRow > counts[35]) return -1;

            int refs = MetadataTableRows.TableStart(streams, methods, counts, 35);
            if (refs < 0) return -1;
            uint assembly = (uint)(refs + (assemblyRow - 1) * (12 + 2 * strings + 2 * blobs));
            uint versionFlags = U32(image, assembly + 8);
            uint key = Index(image, assembly + 12, blobs);
            uint culture = Index(image, assembly + (uint)(12 + blobs + strings), strings);
            if (versionFlags != 0 || key != 0 || culture != 0)
                return -2; // Initial linker supports exact, unsigned, culture-neutral identity.

            reference[0] = (int)assembly;
            reference[1] = (int)Index(image, assembly + (uint)(12 + blobs), strings);
            reference[2] = (int)Index(image, type + (uint)scopeWidth, strings);
            reference[3] = (int)Index(image, type + (uint)(scopeWidth + strings), strings);
            return 0;
        }

        internal static int Matches(byte[] source, int[] sourceHeaps, int[] reference,
            byte[] candidate, int[] heaps, int[] methods)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(candidate, heaps[4], heaps[5], counts) < 0 || counts[32] != 1)
                return -1;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 32);
            if (table < 0) return -1;
            uint row = (uint)table;
            int strings = methods[2], blobs = methods[3];
            int name = (int)Index(candidate, row + (uint)(16 + blobs), strings);
            if (!SameString(source, sourceHeaps, reference[1], candidate, heaps, name))
                return 0;
            uint version = (uint)reference[0];
            if (U32(candidate, row + 4) != U32(source, version) || U32(candidate, row + 8) != U32(source, version + 4))
                return 0;
            if (U32(candidate, row + 12) != 0 || Index(candidate, row + 16, blobs) != 0 ||
                Index(candidate, row + (uint)(16 + blobs + strings), strings) != 0)
                return 0;
            return 1;
        }

        internal static int FindType(byte[] source, int[] sourceHeaps, int[] reference,
            byte[] candidate, int[] heaps, int[] methods, int[] range, bool fields)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(candidate, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            int strings = methods[2];
            int types = TypeRefStart(candidate, heaps, counts, first, strings) + counts[1] * (ScopeWidth(counts) + 2 * strings);
            int fieldIndex = counts[4] < 65536 ? 2 : 4;
            int methodIndex = counts[6] < 65536 ? 2 : 4;
            int methodColumn = 4 + 2 * strings + fieldIndex +
                (counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4);
            int rowWidth = methodColumn + methodIndex;
            int memberColumn = fields ? methodColumn - fieldIndex : methodColumn;
            int memberWidth = fields ? fieldIndex : methodIndex;
            int limit = fields ? counts[4] : counts[6];
            int match = 0;
            for (int i = 0; i < counts[2]; i++)
            {
                uint at = (uint)(types + i * rowWidth);
                if ((U32(candidate, at) & 7) != 1) continue; // Public, top-level type.
                int name = (int)Index(candidate, at + 4, strings);
                int ns = (int)Index(candidate, at + (uint)(4 + strings), strings);
                if (!SameString(source, sourceHeaps, reference[2], candidate, heaps, name) ||
                    !SameString(source, sourceHeaps, reference[3], candidate, heaps, ns))
                    continue;

                uint start = Index(candidate, at + (uint)memberColumn, memberWidth);
                uint end = i == counts[2] - 1 ? (uint)(limit + 1) :
                    Index(candidate, at + (uint)(rowWidth + memberColumn), memberWidth);
                if (start == 0 || end < start || end > (uint)(limit + 1)) return -1;
                if (match != 0) return -5;
                match = i + 1;
                range[0] = (int)start;
                range[1] = (int)end;
            }
            return match == 0 ? -6 : match;
        }

        internal static int FieldTable(byte[] image, int[] streams, int[] methods)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts) < 0) return -1;
            // Field rows immediately precede MethodDef in compressed tables.
            // ReadCounts has already rejected MethodPtr.
            int start = methods[0] - counts[4] * (2 + methods[2] + methods[3]);
            return start < streams[4] ? -1 : start;
        }

        internal static void ReadField(byte[] image, int[] methods, int table, int row, int[] definition)
        {
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (2 + strings + blobs));
            definition[0] = (int)U16(image, at);
            definition[1] = (int)Index(image, at + 2, strings);
            definition[2] = (int)Index(image, at + (uint)(2 + strings), blobs);
        }

        internal static bool SameString(byte[] source, int[] sourceHeaps, int a, byte[] target, int[] targetHeaps, int b)
        {
            int leftSize = sourceHeaps[3], rightSize = targetHeaps[3];
            if (a < 0 || b < 0 || a >= leftSize || b >= rightSize) return false;
            int left = sourceHeaps[2], right = targetHeaps[2];
            while (a < leftSize && b < rightSize)
            {
                byte value = source[left + a++];
                if (target[right + b++] != value) return false;
                if (value == 0) return true;
            }
            return false;
        }

        private static int TypeRefStart(byte[] image, int[] streams, int[] counts, int first, int strings) =>
            first + counts[0] * (2 + strings + 3 * ((image[streams[4] + 6] & 2) == 0 ? 2 : 4));

        private static int ScopeWidth(int[] counts) =>
            counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
