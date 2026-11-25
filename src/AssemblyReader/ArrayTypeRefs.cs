using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ArrayTypeRefs
    {
        internal const byte Bytes = 2, Ints = 3, Packed16 = 7, Singles = 15, Doubles = 16;

        internal static byte Storage(int element)
        {
            if (element == 2 || element == 4 || element == 5) return Bytes;
            if (element == 3 || element == 6 || element == 7) return Packed16;
            if (element == 8 || element == 9) return Ints;
            if (WideStorage.IsIntegerElement(element)) return WideStorage.Array;
            if (element == 0x0c) return Singles;
            if (element == 0x0d) return Doubles;
            return 0;
        }

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
            if (!AssemblyReferences.IsPlatformPrimitiveAssembly(image, streams, methods, counts, (int)(resolution >> 2)))
                return -2;
            uint name = Index(image, type + (uint)scope, strings);
            uint ns = Index(image, type + (uint)(scope + strings), strings);
            uint heapSize = (uint)streams[3];
            if (ns == 0 || name == 0 || !Fits(ns, 7, heapSize) || !Fits(name, 5, heapSize))
                return -1;
            uint baseOffset = (uint)streams[2];
            uint namespaceOffset = baseOffset + ns;
            if (U32(image, namespaceOffset) != 0x74737953 || U16(image, namespaceOffset + 4) != 0x6d65 ||
                image[namespaceOffset + 6] != 0)
                return -2; // System
            if (Named(image, baseOffset, heapSize, name, 4, 0x65747942, 0) || // Byte
                Named(image, baseOffset, heapSize, name, 5, 0x74794253, 0x65) || // SByte
                Named(image, baseOffset, heapSize, name, 7, 0x6c6f6f42, 0x6e6165)) // Boolean
                return Bytes;
            if (Named(image, baseOffset, heapSize, name, 4, 0x72616843, 0) || // Char
                Named(image, baseOffset, heapSize, name, 5, 0x31746e49, 0x36) || // Int16
                Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3631)) // UInt16
                return Packed16;
            if (Named(image, baseOffset, heapSize, name, 5, 0x33746e49, 0x32) || // Int32
                Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3233)) // UInt32
                return Ints;
            if (Named(image, baseOffset, heapSize, name, 5, 0x36746e49, 0x34) || // Int64
                Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3436)) // UInt64
                return WideStorage.Array;
            if (Named(image, baseOffset, heapSize, name, 6, 0x676e6953, 0x656c)) return Singles; // Single
            if (Named(image, baseOffset, heapSize, name, 6, 0x62756f44, 0x656c)) return Doubles; // Double
            return -2;
        }

        // The packed words hold the ASCII spelling without a terminator.
        private static bool Named(byte[] image, uint heap, uint size, uint index, int length, uint first, uint tail)
        {
            if (!Fits(index, (uint)length + 1, size)) return false;
            uint at = heap + index;
            if (U32(image, at) != first || image[at + (uint)length] != 0) return false;
            for (int i = 4; i < length; i++)
                if (image[at + (uint)i] != unchecked((byte)(tail >> ((i - 4) * 8)))) return false;
            return true;
        }

        private static uint Index(byte[] image, uint start, int width) =>
            width == 2 ? U16(image, start) : U32(image, start);
    }
}
