using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // The single CLI Assembly row identifies a loaded module independently of
    // its package basename. Definition keys remain in the original blob heap.
    internal static class AssemblyDefinitionRecord
    {
        internal const int HashAlgorithm = 0, Major = 1, Minor = 2, Build = 3, Revision = 4,
            Flags = 5, Name = 6, Culture = 7, KeyStart = 8, KeyLength = 9, KeyBlob = 10, Width = 11;

        internal static int Read(byte[] image, int[] heaps, int[] layout, int[] fields)
        {
            int table = Find(image, heaps, layout, new int[45]);
            if (table < 0) return table;
            uint row = (uint)table;
            int name = NameIndex(image, layout, row), culture = CultureIndex(image, layout, row);
            if (name <= 0 || name >= heaps[3] || culture < 0 || culture >= heaps[3]) return -1;
            int key = KeyIndex(image, layout, row);
            var start = new int[1];
            int length = StrongNames.ReadKey(image, heaps, key, start);
            if (length < 0) return length;
            int flags = unchecked((int)U32(image, row + 12));
            if (((flags & 1) != 0) != (length != 0)) return -2;

            fields[HashAlgorithm] = unchecked((int)U32(image, row));
            fields[Major] = (int)U16(image, row + 4);
            fields[Minor] = (int)U16(image, row + 6);
            fields[Build] = (int)U16(image, row + 8);
            fields[Revision] = (int)U16(image, row + 10);
            fields[Flags] = flags;
            fields[Name] = name;
            fields[Culture] = culture;
            fields[KeyStart] = start[0];
            fields[KeyLength] = length;
            fields[KeyBlob] = heaps[0] + key;
            return 0;
        }

        internal static int Find(byte[] image, int[] heaps, int[] layout, int[] counts)
        {
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || counts[32] != 1)
                return -1;
            return MetadataTableRows.TableStart(heaps, layout, counts, 32);
        }

        internal static int NameIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + (uint)(16 + layout[3]), layout[2]);

        internal static int CultureIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + (uint)(16 + layout[3] + layout[2]), layout[2]);

        internal static int KeyIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + 16, layout[3]);

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
