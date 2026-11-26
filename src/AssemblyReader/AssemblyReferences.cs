using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class AssemblyReferences
    {
        internal const int SymbolWidth = 13;
        private const int FullKey = 6, KeyStart = 7, KeyLength = 8;
        private const int Leaf = 9;
        internal const int Depth = 10, AssemblyRow = 11, GenericParent = 12;

        // Version byte offset, assembly/type/namespace/member string indexes,
        // signature blob index, then key kind and its validated byte range.
        // The type name slots identify the outer type for forwarder lookup.
        internal static int ReadMember(byte[] image, int[] streams, int[] methods, int row, int[] reference, bool allowClosedParent = false)
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
            // A TypeSpec parent names a closed generic declaring type. The
            // caller resolves its blob against the closed layout graph.
            if (allowClosedParent && (parent & 7) == 4)
            {
                int blob = TypeSpecBlob(image, streams, methods, counts, (int)(parent >> 3));
                if (blob < 0) return blob;
                reference[GenericParent] = blob;
            }
            else
            {
                if ((parent & 7) != 1) return -2;
                int status = ReadTypeReference(image, streams, methods, counts, first, (int)(parent >> 3), reference);
                if (status < 0) return status;
            }
            reference[4] = (int)Index(image, member + (uint)parentWidth, strings);
            reference[5] = (int)Index(image, member + (uint)(parentWidth + strings), blobs);
            return 0;
        }

        internal static int TypeSpecBlob(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts) < 0) return -1;
            return TypeSpecBlob(image, streams, methods, counts, row);
        }

        private static int TypeSpecBlob(byte[] image, int[] streams, int[] methods, int[] counts, int row)
        {
            int table = MetadataTableRows.TableStart(streams, methods, counts, 27);
            if (table < 0 || row < 1 || row > counts[27]) return -1;
            int width = methods[3];
            int index = (int)Index(image, (uint)(table + (row - 1) * width), width);
            return index > 0 && index < streams[1] ? index : -1;
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
            int strings = methods[2];
            int scopeWidth = ScopeWidth(counts);
            int typeRefWidth = scopeWidth + 2 * strings;
            int typeRefs = TypeRefStart(image, streams, counts, first, strings);
            reference[Leaf] = typeRow;
            for (int depth = 0; depth < counts[1]; depth++)
            {
                if (typeRow < 1 || typeRow > counts[1]) return -1;
                uint type = (uint)(typeRefs + (typeRow - 1) * typeRefWidth);
                uint scope = Index(image, type, scopeWidth);
                int scopeRow = (int)(scope >> 2);
                if ((scope & 3) == 2)
                {
                    reference[Depth] = depth;
                    reference[2] = (int)Index(image, type + (uint)scopeWidth, strings);
                    reference[3] = (int)Index(image, type + (uint)(scopeWidth + strings), strings);
                    return ReadAssemblyRow(image, streams, methods, counts, scopeRow, reference);
                }
                if ((scope & 3) != 3) return -2;
                typeRow = scopeRow;
            }
            return -1; // The enclosing TypeRef chain has a cycle.
        }

        internal static int ReadAssembly(byte[] image, int[] streams, int[] methods, int row, int[] reference)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts) < 0) return -1;
            return ReadAssemblyRow(image, streams, methods, counts, row, reference);
        }

        private static int ReadAssemblyRow(byte[] image, int[] streams, int[] methods, int[] counts,
            int assemblyRow, int[] reference)
        {
            if (assemblyRow < 1 || assemblyRow > counts[35]) return -1;
            int strings = methods[2], blobs = methods[3];

            int refs = MetadataTableRows.TableStart(streams, methods, counts, 35);
            if (refs < 0) return -1;
            uint assembly = (uint)(refs + (assemblyRow - 1) * (12 + 2 * strings + 2 * blobs));
            uint versionFlags = U32(image, assembly + 8);
            uint key = Index(image, assembly + 12, blobs);
            uint culture = Index(image, assembly + (uint)(12 + blobs + strings), strings);
            if ((versionFlags & ~1u) != 0 || culture != 0) return -2;
            var data = new int[1];
            int length = StrongNames.ReadKey(image, streams, (int)key, data);
            if (length < 0) return length;
            bool fullKey = (versionFlags & 1) != 0;
            if (fullKey ? length == 0 : length != 0 && length != 8) return -1;
            reference[FullKey] = fullKey ? 1 : 0;
            reference[KeyStart] = data[0];
            reference[KeyLength] = length;

            reference[0] = (int)assembly;
            reference[1] = (int)Index(image, assembly + (uint)(12 + blobs), strings);
            reference[AssemblyRow] = assemblyRow;
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
            uint flags = U32(candidate, row + 12);
            uint key = Index(candidate, row + 16, blobs);
            if ((flags & ~1u) != 0 || Index(candidate, row + (uint)(16 + blobs + strings), strings) != 0)
                return 0;
            if ((flags & 1) == 0) return key == 0 && reference[KeyLength] == 0 ? 1 : 0;
            if (reference[KeyLength] == 0) return 0;
            var data = new int[1];
            int length = StrongNames.ReadKey(candidate, heaps, (int)key, data);
            if (length <= 0) return -1;
            if (reference[FullKey] != 0)
                return length == reference[KeyLength] &&
                    StrongNames.Equal(source, reference[KeyStart], candidate, data[0], length) ? 1 : 0;
            return StrongNames.MatchesToken(source, reference[KeyStart], candidate, data[0], length) ? 1 : 0;
        }

        internal static int FindType(byte[] source, int[] sourceHeaps, int[] sourceMethods, int[] reference,
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
            int visibility = 0;
            for (int i = 0; i < counts[2]; i++)
            {
                uint at = (uint)(types + i * rowWidth);
                int access = (int)(U32(candidate, at) & 7);
                if (access > 1) continue; // Nested definitions use the NestedClass table.
                int name = (int)Index(candidate, at + 4, strings);
                int ns = (int)Index(candidate, at + (uint)(4 + strings), strings);
                if (!SameString(source, sourceHeaps, reference[2], candidate, heaps, name) ||
                    !SameString(source, sourceHeaps, reference[3], candidate, heaps, ns))
                    continue;

                if (match != 0) return -5;
                match = i + 1;
                visibility = access;
            }
            if (match == 0) return -6;
            int friend = -1;
            if (visibility == 0)
            {
                friend = FriendAssemblies.Allows(candidate, heaps, methods, counts, source, sourceHeaps, sourceMethods);
                if (friend <= 0) return friend == 0 ? -2 : friend;
            }
            if (reference[Depth] != 0)
            {
                int[] names = NestedNames(source, sourceHeaps, sourceMethods, reference);
                match = NestedTypes.Find(candidate, heaps, methods, counts, types, rowWidth, match, names, source, sourceHeaps);
                if (match < 0) return match;
                // NestedTypes encodes whether any enclosing definition needs
                // assembly access, after checking the entire containment path.
                if ((match & 1) != 0 && friend != 1)
                {
                    friend = FriendAssemblies.Allows(candidate, heaps, methods, counts, source, sourceHeaps, sourceMethods);
                    if (friend <= 0) return friend == 0 ? -2 : friend;
                }
                match >>= 1;
            }
            uint row = (uint)(types + (match - 1) * rowWidth + memberColumn);
            uint start = Index(candidate, row, memberWidth);
            uint end = match == counts[2] ? (uint)(limit + 1) : Index(candidate, row + (uint)rowWidth, memberWidth);
            if (start == 0 || end < start || end > (uint)(limit + 1)) return -1;
            range[0] = (int)start;
            range[1] = (int)end;
            return match;
        }

        private static int[] NestedNames(byte[] image, int[] heaps, int[] methods, int[] reference)
        {
            var names = new int[reference[Depth] * 2];
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int strings = methods[2], scopeWidth = ScopeWidth(counts);
            int start = TypeRefStart(image, heaps, counts, first, strings);
            int row = reference[Leaf];
            // Preserve both name columns for each child in outer-to-inner order.
            // ReadTypeReference has already validated the enclosing row chain.
            for (int i = names.Length - 2; i >= 0; i -= 2)
            {
                uint at = (uint)(start + (row - 1) * (scopeWidth + 2 * strings));
                names[i] = (int)Index(image, at + (uint)scopeWidth, strings);
                names[i + 1] = (int)Index(image, at + (uint)(scopeWidth + strings), strings);
                row = (int)(Index(image, at, scopeWidth) >> 2);
            }
            return names;
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
