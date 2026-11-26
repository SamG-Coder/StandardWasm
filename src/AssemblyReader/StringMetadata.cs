using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class StringMetadata
    {
        internal const int Length = 1, Character = 2, Concat = 3, FromChars = 4;

        internal static int CopyLiteral(byte[] image, int[] streams, int tokenOffset, byte[] bytes, int used)
        {
            if (tokenOffset < 0 || tokenOffset >= streams[7])
                return -1;
            if (tokenOffset == 0)
                return 0; // The reserved first heap entry is empty.
            int first = streams[6] + tokenOffset, end = streams[6] + streams[7];
            var next = new int[1];
            int size = CompressedUnsigned(image, first, end, next);
            int start = next[0];
            if (size < 1 || (size & 1) == 0 || size > end - start || image[start + size - 1] > 1)
                return -1;
            int byteCount = size - 1;
            if (byteCount > bytes.Length - used)
                return -3;
            // #US is UTF-16 little endian, matching Wasm packed i16 data.
            // The terminal flag is metadata and is not part of the string.
            for (int i = 0; i < byteCount; i++)
                bytes[used + i] = image[start + i];
            return byteCount / 2;
        }

        internal static int Member(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row < 1 || row > counts[10])
                return 0;
            int table = MetadataTableRows.TableStart(streams, methods, counts, 10);
            if (table < 0) return 0;
            int strings = methods[2], blobs = methods[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(table + (row - 1) * width);
            int parentWidth = width - strings - blobs;
            uint parent = Index(image, at, parentWidth);
            int refRow = (int)(parent >> 3);
            if ((parent & 7) != 1 || refRow < 1 || refRow > counts[1])
                return 0;

            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guid);
            int scopeWidth = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            uint type = (uint)(refs + (refRow - 1) * (scopeWidth + 2 * strings));
            uint scope = Index(image, type, scopeWidth);
            if ((scope & 3) != 2 || scope >> 2 == 0 || scope >> 2 > (uint)counts[35])
                return 0;
            uint typeName = Index(image, type + (uint)scopeWidth, strings);
            uint typeNamespace = Index(image, type + (uint)(scopeWidth + strings), strings);
            if (!Fits(typeName, 7, (uint)streams[3]) || !Fits(typeNamespace, 7, (uint)streams[3])) return 0;
            uint n = (uint)streams[2] + typeName, s = (uint)streams[2] + typeNamespace;
            if (U32(image, n) != 0x69727453 || U16(image, n + 4) != 0x676e || image[n + 6] != 0 ||
                U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0 ||
                !PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, (int)(scope >> 2)))
                return 0;

            uint name = Index(image, at + (uint)parentWidth, strings);
            uint sig = Index(image, at + (uint)(parentWidth + strings), blobs);
            if (!Fits(name, 6, (uint)streams[3]) || !Fits(sig, 4, (uint)streams[1])) return 0;
            n = (uint)streams[2] + name;
            uint signature = (uint)streams[0] + sig;
            if (Fits(name, 7, (uint)streams[3]) && U32(image, n) == 0x636e6f43 &&
                U16(image, n + 4) == 0x7461 && image[n + 6] == 0 &&
                Fits(sig, 6, (uint)streams[1]) && U32(image, signature) == 0x0e020005 &&
                U16(image, signature + 4) == 0x0e0e)
                return Concat;
            if (U32(image, n) == 0x6f74632e && U16(image, n + 4) == 0x72 &&
                Fits(sig, 6, (uint)streams[1]) && U32(image, signature) == 0x01012005 &&
                U16(image, signature + 4) == 0x031d)
                return FromChars;
            if (!Fits(name, 10, (uint)streams[3])) return 0;
            if (U32(image, n) != 0x5f746567) return 0; // get_
            if (Fits(name, 11, (uint)streams[3]) && U32(image, n + 4) == 0x676e654c &&
                U16(image, n + 8) == 0x6874 && image[n + 10] == 0 &&
                U32(image, signature) == 0x08002003)
                return Length;
            if (U32(image, n + 4) == 0x72616843 && U16(image, n + 8) == 0x73 &&
                Fits(sig, 5, (uint)streams[1]) && U32(image, signature) == 0x03012004 &&
                image[signature + 4] == 8)
                return Character;
            return 0;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
