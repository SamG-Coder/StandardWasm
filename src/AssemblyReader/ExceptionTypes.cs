using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExceptionTypes
    {
        internal const int Overflow = 1, DivideByZero = 2, Arithmetic = Overflow | DivideByZero;

        internal static int ArithmeticMask(byte[] image, int[] heaps, int[] methods, int token)
        {
            if ((uint)token >> 24 != 1) return 0;
            int row = token & 0xffffff;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[1]) return 0;
            int strings = methods[2], guids = (image[heaps[4] + 6] & 2) == 0 ? 2 : 4;
            int width = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guids);
            uint type = (uint)(refs + (row - 1) * (width + 2 * strings));
            uint scope = Index(image, type, width);
            if ((scope & 3) != 2 || !AssemblyReferences.IsPlatformPrimitiveAssembly(image, heaps, methods, counts, (int)(scope >> 2)))
                return 0;
            uint name = Index(image, type + (uint)width, strings);
            uint ns = Index(image, type + (uint)(width + strings), strings);
            if (!Fits(ns, 7, (uint)heaps[3])) return 0;
            uint n = (uint)heaps[2] + name, s = (uint)heaps[2] + ns, limit = (uint)heaps[3];
            if (U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0)
                return 0;

            // Match little-endian ASCII type names, including their terminators.
            if (Fits(name, 18, limit) && U32(image, n) == 0x7265764f && U32(image, n + 4) == 0x776f6c66 &&
                U32(image, n + 8) == 0x65637845 && U32(image, n + 12) == 0x6f697470 && U16(image, n + 16) == 0x006e)
                return Overflow;
            if (Fits(name, 22, limit) && U32(image, n) == 0x69766944 && U32(image, n + 4) == 0x79426564 &&
                U32(image, n + 8) == 0x6f72655a && U32(image, n + 12) == 0x65637845 &&
                U32(image, n + 16) == 0x6f697470 && U16(image, n + 20) == 0x006e)
                return DivideByZero;
            if (Fits(name, 20, limit) && U32(image, n) == 0x74697241 && U32(image, n + 4) == 0x74656d68 &&
                U32(image, n + 8) == 0x78456369 && U32(image, n + 12) == 0x74706563 && U32(image, n + 16) == 0x006e6f69)
                return Arithmetic;
            return 0;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
