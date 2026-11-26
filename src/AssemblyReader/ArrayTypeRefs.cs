using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ArrayTypeRefs
    {
        internal static int ElementType(byte[] image, int[] streams, int[] methods, uint token)
        {
            int row = (int)(token & 0xffffff);
            if (token >> 24 != 1 || row == 0)
                return -2;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row > counts[1])
                return -1;
            int strings = methods[2];
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int moduleWidth = 2 + strings + 3 * guid;
            int scope = counts[0] < 16384 && counts[26] < 16384 && counts[35] < 16384 && counts[1] < 16384 ? 2 : 4;
            // MethodDef discovery already validated every preceding row.
            uint type = (uint)(first + counts[0] * moduleWidth + (row - 1) * (scope + 2 * strings));
            uint resolution = Index(image, type, scope);
            if ((resolution & 3) != 2 || resolution >> 2 == 0 || resolution >> 2 > (uint)counts[35])
                return -2; // Only AssemblyRef-scoped primitive TypeRefs.
            uint name = Index(image, type + (uint)scope, strings);
            uint ns = Index(image, type + (uint)(scope + strings), strings);
            uint heapSize = (uint)streams[3];
            if (!Fits(ns, 7, heapSize) || !Fits(name, 5, heapSize))
                return -1;
            uint baseOffset = (uint)streams[2];
            uint namespaceOffset = baseOffset + ns;
            if (U32(image, namespaceOffset) != 0x74737953 || U16(image, namespaceOffset + 4) != 0x6d65 ||
                image[namespaceOffset + 6] != 0)
                return -2; // System
            uint entry = baseOffset + name;
            uint prefix = U32(image, entry);
            if (prefix == 0x65747942 && image[entry + 4] == 0)
                return 2; // Byte
            if (prefix == 0x33746e49 && Fits(name, 6, heapSize) && U16(image, entry + 4) == 0x32)
                return 3; // Int32
            if (prefix == 0x72616843 && image[entry + 4] == 0)
                return 7; // Char
            if (prefix == 0x36746e49 && Fits(name, 6, heapSize) && U16(image, entry + 4) == 0x34)
                return WideStorage.Array; // Int64
            if (prefix == 0x746e4955 && Fits(name, 7, heapSize) && U16(image, entry + 4) == 0x3436 &&
                image[entry + 6] == 0)
                return WideStorage.Array; // UInt64
            return -2;
        }

        private static uint Index(byte[] image, uint start, int width) =>
            width == 2 ? U16(image, start) : U32(image, start);
    }
}
