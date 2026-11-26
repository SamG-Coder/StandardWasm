using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExceptionTypes
    {
        internal const int Overflow = 1, DivideByZero = 2, Arithmetic = Overflow | DivideByZero;

        internal const int IndexOutOfRange = 4, NullReference = 8, InvalidCast = 16, Argument = 32;
        internal const int ArgumentNull = 128;
        internal const int Supported = Arithmetic | IndexOutOfRange | NullReference | InvalidCast | Argument | ArgumentNull;

        // A thrown managed object (Stage 126, decision 0054) travels on its own tag.
        // A clause that can receive it also carries its class type above the tag bits.
        internal const int Object = 64, ClassShift = 8;

        internal static int CatchMask(byte[] image, int[] heaps, int[] methods, int token)
        {
            uint table = (uint)token >> 24;
            if (table != 1 && table != 2) return 0;
            int row = token & 0xffffff;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[table == 1 ? 1 : 2]) return 0;
            int strings = methods[2], guids = (image[heaps[4] + 6] & 2) == 0 ? 2 : 4;
            int width = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guids);
            uint name, ns;
            if (table == 1)
            {
                // A platform TypeRef names the authentic System exception type.
                uint type = (uint)(refs + (row - 1) * (width + 2 * strings));
                uint scope = Index(image, type, width);
                if ((scope & 3) != 2 || !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts, (int)(scope >> 2)))
                    return 0;
                name = Index(image, type + (uint)width, strings);
                ns = Index(image, type + (uint)(width + strings), strings);
            }
            else
            {
                // Inside the authentic CoreLib the same exception types are TypeDefs. Only that
                // image may map them to fixed tags; a same-named user TypeDef stays an object class.
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return 0;
                int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
                int rowWidth = 4 + 2 * strings + extends + (counts[4] < 65536 ? 2 : 4) + (counts[6] < 65536 ? 2 : 4);
                uint type = (uint)(refs + counts[1] * (width + 2 * strings) + (row - 1) * rowWidth);
                name = Index(image, type + 4, strings);
                ns = Index(image, type + (uint)(4 + strings), strings);
            }
            if (!Fits(ns, 7, (uint)heaps[3])) return 0;
            uint n = (uint)heaps[2] + name, s = (uint)heaps[2] + ns, limit = (uint)heaps[3];
            if (U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0)
                return 0;

            // Match little-endian ASCII type names, including their terminators.
            // C# encodes a bare catch as a catch of the platform System.Object.
            if (Fits(name, 7, limit) && U32(image, n) == 0x656a624f &&
                U16(image, n + 4) == 0x7463 && image[n + 6] == 0)
                return Supported;
            // All currently emitted managed tags represent subclasses of SystemException.
            // Dispatch still catches only those tags, so engine traps stay outside this mask.
            if (Fits(name, 10, limit) && U32(image, n) == 0x65637845 &&
                U32(image, n + 4) == 0x6f697470 && U16(image, n + 8) == 0x006e)
                return Supported;
            if (Fits(name, 16, limit) && U32(image, n) == 0x74737953 &&
                U32(image, n + 4) == 0x78456d65 && U32(image, n + 8) == 0x74706563 &&
                U32(image, n + 12) == 0x006e6f69)
                return Supported;
            if (MatchesAscii(image, heaps, name, "ArgumentNullException"))
                return ArgumentNull;
            if (Fits(name, 18, limit) && U32(image, n) == 0x75677241 && U32(image, n + 4) == 0x746e656d &&
                U32(image, n + 8) == 0x65637845 && U32(image, n + 12) == 0x6f697470 && U16(image, n + 16) == 0x006e)
                return Argument | ArgumentNull;
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
            if (Fits(name, 25, limit) && U32(image, n) == 0x65646e49 && U32(image, n + 4) == 0x74754f78 &&
                U32(image, n + 8) == 0x6152664f && U32(image, n + 12) == 0x4565676e &&
                U32(image, n + 16) == 0x70656378 && U32(image, n + 20) == 0x6e6f6974 && image[n + 24] == 0)
                return IndexOutOfRange;
            if (Fits(name, 23, limit) && U32(image, n) == 0x6c6c754e && U32(image, n + 4) == 0x65666552 &&
                U32(image, n + 8) == 0x636e6572 && U32(image, n + 12) == 0x63784565 &&
                U32(image, n + 16) == 0x69747065 && U16(image, n + 20) == 0x6e6f && image[n + 22] == 0)
                return NullReference;
            if (Fits(name, 21, limit) && U32(image, n) == 0x61766e49 && U32(image, n + 4) == 0x4364696c &&
                U32(image, n + 8) == 0x45747361 && U32(image, n + 12) == 0x70656378 &&
                U32(image, n + 16) == 0x6e6f6974 && image[n + 20] == 0)
                return InvalidCast;
            return 0;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
