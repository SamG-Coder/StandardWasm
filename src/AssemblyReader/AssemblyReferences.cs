using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class AssemblyReferences
    {
        // Reference descriptor: version byte offset, assembly/name/namespace/method string indexes, signature blob index.
        internal static int ReadMethod(byte[] image, int[] streams, int[] methods, int row, int[] reference)
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
            int typeRow = (int)(parent >> 3);
            if ((parent & 7) != 1) return -2; // TypeRef only.
            if (typeRow < 1 || typeRow > counts[1]) return -1;

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
            reference[4] = (int)Index(image, member + (uint)parentWidth, strings);
            reference[5] = (int)Index(image, member + (uint)(parentWidth + strings), blobs);
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

        internal static int FindMethod(byte[] source, int[] sourceHeaps, int[] reference,
            byte[] candidate, int[] heaps, int[] methods)
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

                uint start = Index(candidate, at + (uint)methodColumn, methodIndex);
                uint end = i == counts[2] - 1 ? (uint)(counts[6] + 1) :
                    Index(candidate, at + (uint)(rowWidth + methodColumn), methodIndex);
                if (start == 0 || end < start || end > (uint)(counts[6] + 1)) return -1;
                for (int method = (int)start; method < (int)end; method++)
                {
                    int flags = MethodDefinitions.ReadColumn(candidate, methods, method, 2);
                    if ((flags & 0x17) != 0x16) continue; // Public static.
                    int methodName = MethodDefinitions.ReadColumn(candidate, methods, method, 3);
                    int sig = MethodDefinitions.ReadColumn(candidate, methods, method, 4);
                    if (!SameString(source, sourceHeaps, reference[4], candidate, heaps, methodName) ||
                        !SameSignature(source, sourceHeaps, reference[5], candidate, heaps, sig))
                        continue;
                    if (match != 0) return -5;
                    match = method;
                }
            }
            return match == 0 ? -6 : match;
        }

        private static bool SameString(byte[] source, int[] sourceHeaps, int a, byte[] target, int[] targetHeaps, int b)
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

        private static bool SameSignature(byte[] source, int[] sourceHeaps, int a, byte[] target, int[] targetHeaps, int b)
        {
            if (a < 1 || b < 1 || a >= sourceHeaps[1] || b >= targetHeaps[1]) return false;
            int limitA = sourceHeaps[0] + sourceHeaps[1], limitB = targetHeaps[0] + targetHeaps[1];
            var next = new int[1];
            int sizeA = CompressedUnsigned(source, sourceHeaps[0] + a, limitA, next);
            int startA = next[0];
            int sizeB = CompressedUnsigned(target, targetHeaps[0] + b, limitB, next);
            int startB = next[0];
            if (sizeA < 3 || sizeA != sizeB || sizeA > limitA - startA || sizeA > limitB - startB)
                return false;
            if (source[startA] != 0) return false;
            for (int i = 0; i < sizeA; i++)
                if (source[startA + i] != target[startB + i]) return false;
            return true;
        }

        private static int TypeRefStart(byte[] image, int[] streams, int[] counts, int first, int strings) =>
            first + counts[0] * (2 + strings + 3 * ((image[streams[4] + 6] & 2) == 0 ? 2 : 4));

        private static int ScopeWidth(int[] counts) =>
            counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
