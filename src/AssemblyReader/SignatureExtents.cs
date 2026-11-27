using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // Finds where an encoded signature element ends (ECMA-335 II.23.2) without resolving
    // any type. A function pointer's own signature (ELEMENT_TYPE_FNPTR) is skipped this way,
    // because its value is a native-size code address whatever it points to (decision 0078).
    internal static class SignatureExtents
    {
        private const int MaximumDepth = 64; // bounds recursion on malformed nesting

        // Returns the offset just past the type at cursor, or -1 if it is malformed.
        internal static int Type(byte[] image, int cursor, int end) => Type(image, cursor, end, 0, false);

        // Returns the offset just past a MethodDefSig, MethodRefSig or StandAloneMethodSig.
        internal static int Method(byte[] image, int cursor, int end) => Method(image, cursor, end, 0, false);

        // As Method, but -1 if any element names a type token (CLASS, VALUETYPE, a custom
        // modifier or a generic instantiation). Such a signature is identical in every image,
        // so two of them match exactly when their bytes do.
        internal static int TokenFreeMethod(byte[] image, int cursor, int end) => Method(image, cursor, end, 0, true);

        private static int Method(byte[] image, int cursor, int end, int depth, bool tokenFree)
        {
            if (depth > MaximumDepth || cursor >= end) return -1;
            var next = new int[1];
            int convention = image[cursor++];
            if ((convention & 0x10) != 0)
            {
                if (CompressedUnsigned(image, cursor, end, next) < 0) return -1;
                cursor = next[0];
            }
            int count = CompressedUnsigned(image, cursor, end, next);
            if (count < 0) return -1;
            cursor = Type(image, next[0], end, depth + 1, tokenFree); // return type
            for (int i = 0; i < count && cursor >= 0; i++)
            {
                if (cursor < end && image[cursor] == 0x41) cursor++; // SENTINEL before vararg extras
                cursor = Type(image, cursor, end, depth + 1, tokenFree);
            }
            return cursor;
        }

        private static int Type(byte[] image, int cursor, int end, int depth, bool tokenFree)
        {
            if (depth > MaximumDepth || cursor < 0 || cursor >= end) return -1;
            var next = new int[1];
            int element = image[cursor++];
            if (element >= 0x01 && element <= 0x0e || element == 0x16 || element == 0x18 ||
                element == 0x19 || element == 0x1c)
                return cursor;
            if (element == 0x0f || element == 0x10 || element == 0x1d || element == 0x45)
                return Type(image, cursor, end, depth + 1, tokenFree);
            if (tokenFree && (element == 0x11 || element == 0x12 || element == 0x15 || element == 0x1f ||
                element == 0x20 || element == 0x14)) return -1;
            if (element == 0x11 || element == 0x12 || element == 0x13 || element == 0x1e)
                return CompressedUnsigned(image, cursor, end, next) < 0 ? -1 : next[0];
            if (element == 0x1f || element == 0x20) // custom modifier, then the modified type
                return CompressedUnsigned(image, cursor, end, next) < 0 ? -1 : Type(image, next[0], end, depth + 1, tokenFree);
            if (element == 0x1b) return Method(image, cursor, end, depth + 1, tokenFree);
            if (element == 0x15)
            {
                cursor = Type(image, cursor, end, depth + 1, tokenFree);
                int arguments = cursor < 0 ? -1 : CompressedUnsigned(image, cursor, end, next);
                if (arguments < 0) return -1;
                cursor = next[0];
                for (int i = 0; i < arguments && cursor >= 0; i++) cursor = Type(image, cursor, end, depth + 1, tokenFree);
                return cursor;
            }
            if (element == 0x14) // ARRAY type rank NumSizes Size* NumLoBounds LoBound*
            {
                cursor = Type(image, cursor, end, depth + 1, tokenFree);
                if (cursor < 0 || CompressedUnsigned(image, cursor, end, next) < 0) return -1;
                cursor = next[0];
                for (int list = 0; list < 2; list++)
                {
                    int items = CompressedUnsigned(image, cursor, end, next);
                    if (items < 0) return -1;
                    cursor = next[0];
                    for (int i = 0; i < items; i++)
                    {
                        // Sizes are unsigned; lower bounds are signed with the same width rules.
                        if (CompressedUnsigned(image, cursor, end, next) < 0) return -1;
                        cursor = next[0];
                    }
                }
                return cursor;
            }
            return -1;
        }
    }
}
