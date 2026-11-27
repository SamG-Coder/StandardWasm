using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class NestedTypes
    {
        // The enclosing TypeDef row of a nested type, or 0 for a top-level type (NestedClass, II.22.32).
        internal static int Enclosing(byte[] image, int[] heaps, int[] methods, int[] counts, int child)
        {
            if (child < 1 || child > counts[2] || counts[41] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 41);
            if (table < 0) return 0;
            int indexWidth = counts[2] < 65536 ? 2 : 4;
            for (int row = 0; row < counts[41]; row++)
            {
                uint entry = (uint)(table + row * 2 * indexWidth);
                if (Index(image, entry, indexWidth) == child) return Index(image, entry + (uint)indexWidth, indexWidth);
            }
            return 0;
        }

        internal static bool IsDirectChild(byte[] image, int[] heaps, int[] methods, int[] counts, int child, int parent)
        {
            if (child < 1 || child > counts[2] || parent < 1 || parent > counts[2] || child == parent ||
                counts[41] == 0) return false;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 41);
            if (table < 0) return false;
            int indexWidth = counts[2] < 65536 ? 2 : 4;
            int enclosing = 0;
            for (int row = 0; row < counts[41]; row++)
            {
                uint entry = (uint)(table + row * 2 * indexWidth);
                if (Index(image, entry, indexWidth) != child) continue;
                if (enclosing != 0) return false;
                enclosing = Index(image, entry + (uint)indexWidth, indexWidth);
                if (enclosing != parent) return false;
            }
            return enclosing == parent;
        }

        // Return (leaf row << 1) | internal-access-required. Resolve each
        // name under its exact parent; an equal leaf elsewhere cannot bind.
        internal static int Find(byte[] image, int[] heaps, int[] layout, int[] counts, int types, int typeWidth,
            int parent, int[] names, byte[] source, int[] sourceHeaps)
        {
            if (counts[41] == 0) return -6;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 41);
            if (table < 0) return -1;
            int strings = layout[2], indexWidth = counts[2] < 65536 ? 2 : 4;
            bool internalAccess = false;
            for (int i = 0; i < names.Length; i += 2)
            {
                int match = 0, access = 0;
                for (int row = 0; row < counts[41]; row++)
                {
                    uint entry = (uint)(table + row * 2 * indexWidth);
                    if (Index(image, entry + (uint)indexWidth, indexWidth) != parent) continue;
                    int child = Index(image, entry, indexWidth);
                    if (child < 1 || child > counts[2] || child == parent) return -1;
                    uint at = (uint)(types + (child - 1) * typeWidth);
                    int name = Index(image, at + 4, strings);
                    int space = Index(image, at + (uint)(4 + strings), strings);
                    if (!AssemblyReferences.SameString(source, sourceHeaps, names[i], image, heaps, name) ||
                        !AssemblyReferences.SameString(source, sourceHeaps, names[i + 1], image, heaps, space))
                        continue;
                    if (match != 0) return -5;
                    match = child;
                    access = (int)(U32(image, at) & 7);
                }
                if (match == 0) return -6;
                if (access == 5 || access == 7) internalAccess = true; // Assembly or FamilyOrAssembly.
                else if (access != 2) return -2; // Other cross-assembly access needs caller-type context.
                parent = match;
            }
            return (parent << 1) | (internalAccess ? 1 : 0);
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
