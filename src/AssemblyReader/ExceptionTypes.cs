using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExceptionTypes
    {
        internal static bool IsOverflow(byte[] image, int[] heaps, int[] methods, int token)
        {
            if ((uint)token >> 24 != 1) return false;
            int row = token & 0xffffff;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[1]) return false;
            int strings = methods[2], guids = (image[heaps[4] + 6] & 2) == 0 ? 2 : 4;
            int width = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guids);
            uint type = (uint)(refs + (row - 1) * (width + 2 * strings));
            uint scope = Index(image, type, width);
            if ((scope & 3) != 2 || !AssemblyReferences.IsPlatformPrimitiveAssembly(image, heaps, methods, counts, (int)(scope >> 2)))
                return false;
            uint name = Index(image, type + (uint)width, strings);
            uint ns = Index(image, type + (uint)(width + strings), strings);
            if (!Fits(name, 18, (uint)heaps[3]) || !Fits(ns, 7, (uint)heaps[3])) return false;
            uint n = (uint)heaps[2] + name, s = (uint)heaps[2] + ns;
            // System.OverflowException, including both string terminators.
            return U32(image, s) == 0x74737953 && U16(image, s + 4) == 0x6d65 && image[s + 6] == 0 &&
                U32(image, n) == 0x7265764f && U32(image, n + 4) == 0x776f6c66 &&
                U32(image, n + 8) == 0x65637845 && U32(image, n + 12) == 0x6f697470 && U16(image, n + 16) == 0x006e;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
