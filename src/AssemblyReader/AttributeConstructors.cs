using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class AttributeConstructors
    {
        // Return a TypeDef or TypeRef encoded as a MemberRef parent.
        internal static int ReadType(byte[] image, int[] heaps, int[] methods, int[] counts, int constructor)
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
            int parent = ReadRowIndex(image, (uint)(table + (row - 1) * width), width - methods[2] - methods[3]);
            int tag = parent & 7;
            if (tag > 1) return 0;
            int typeRow = parent >> 3;
            return typeRow > 0 && typeRow <= counts[tag == 1 ? 1 : 2] ? parent : -1;
        }

        internal static int ReadRowIndex(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
