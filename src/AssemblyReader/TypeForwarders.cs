using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class TypeForwarders
    {
        // Only top-level forwarders whose implementation is an AssemblyRef
        // have the same identity model as a top-level TypeRef.
        internal static int Find(byte[] source, int[] sourceHeaps, int[] reference,
            byte[] facade, int[] heaps, int[] layout)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(facade, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[39] == 0) return -6;
            int start = MetadataTableRows.TableStart(heaps, layout, counts, 39);
            if (start < 0) return -1;
            int strings = layout[2];
            int implementationWidth = counts[38] < 16384 && counts[35] < 16384 && counts[39] < 16384 ? 2 : 4;
            int width = 8 + 2 * strings + implementationWidth;
            int match = 0;
            for (int row = 0; row < counts[39]; row++)
            {
                uint at = (uint)(start + row * width);
                if ((U32(facade, at) & 0x00200000) == 0) continue;
                int name = (int)Index(facade, at + 8, strings);
                int ns = (int)Index(facade, at + (uint)(8 + strings), strings);
                if (!AssemblyReferences.SameString(source, sourceHeaps, reference[2], facade, heaps, name) ||
                    !AssemblyReferences.SameString(source, sourceHeaps, reference[3], facade, heaps, ns))
                    continue;
                uint implementation = Index(facade, at + (uint)(8 + 2 * strings), implementationWidth);
                if ((implementation & 3) != 1) return -2;
                int assembly = (int)(implementation >> 2);
                if (assembly < 1 || assembly > counts[35]) return -1;
                if (match != 0) return -5;
                match = assembly;
            }
            return match == 0 ? -6 : match;
        }

        private static uint Index(byte[] image, uint offset, int width) =>
            width == 2 ? U16(image, offset) : U32(image, offset);
    }
}
