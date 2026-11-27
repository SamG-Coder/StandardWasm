using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class SignatureReader
    {
        internal const int Boolean = 0x02, Int32 = 0x08;
        private const int ValueType = 0x11, Class = 0x12, TypeParameter = 0x13, GenericInstance = 0x15;
        private const int Instance = 0x20;

        // Reads an ordinary instance call header. The cursor points to the
        // return type on success and the result marks the end of the blob.
        internal static int ReadInstance(byte[] image, int[] heaps, int blob, int arity, int[] cursor)
        {
            if (blob < 1 || blob >= heaps[1]) return -1;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int at = cursor[0];
            if (size < 3 || size > limit - at || image[at] != Instance) return -1;
            int end = at + size;
            int count = CompressedUnsigned(image, at + 1, end, cursor);
            return count == arity && count < end - cursor[0] ? end : -1;
        }

        internal static int ReadGenericHeader(byte[] image, int[] cursor, int end, int[] head)
        {
            int start = cursor[0];
            if (start >= end) return -1;
            int definitionKind = image[start];
            if (definitionKind != Class && definitionKind != ValueType) return -2;
            int definition = CompressedUnsigned(image, start + 1, end, cursor);
            if (definition <= 0) return -1;
            int count = CompressedUnsigned(image, cursor[0], end, cursor);
            if (count <= 0 || count > end - cursor[0]) return -1;
            head[0] = definition;
            head[1] = definitionKind;
            return count;
        }

        // Reads a value-type instantiation with one exact declaring type
        // parameter, e.g. a return type of SomeValue<!0>.
        internal static int ReadGenericValue(byte[] image, int[] cursor, int end, int parameter)
        {
            if (!TakeElement(image, cursor, end, GenericInstance)) return -1;
            var head = new int[2];
            if (ReadGenericHeader(image, cursor, end, head) != 1 || head[1] != ValueType ||
                !TakeElement(image, cursor, end, TypeParameter)) return -1;
            return CompressedUnsigned(image, cursor[0], end, cursor) == parameter ? head[0] : -1;
        }

        internal static bool TakeElement(byte[] image, int[] cursor, int end, int element)
        {
            int at = cursor[0];
            if (at >= end || image[at] != element) return false;
            cursor[0] = at + 1;
            return true;
        }
    }
}
