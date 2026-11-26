using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class DelegateMetadata
    {
        internal const int Construct = 1, Invoke = 2;

        internal static int Member(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return 0;
            int strings = methods[2], blobs = methods[3];
            int starts = MetadataTableRows.TableStart(streams, methods, counts, 10);
            if (starts < 0) return 0;
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(starts + (row - 1) * width);
            int parentWidth = width - strings - blobs;
            uint parent = Index(image, at, parentWidth);
            int typeSpec = (int)(parent >> 3);
            if ((parent & 7) != 4 || typeSpec < 1 || typeSpec > counts[27]) return 0;
            int specs = MetadataTableRows.TableStart(streams, methods, counts, 27);
            if (specs < 0) return 0;
            uint typeSig = Index(image, (uint)(specs + (typeSpec - 1) * blobs), blobs);
            int heapEnd = streams[0] + streams[1];
            if (typeSig >= (uint)streams[1]) return 0;
            int typeStart = streams[0] + (int)typeSig;
            int typeSize = image[typeStart];
            if (typeSize < 5 || typeSize >= heapEnd - typeStart ||
                ParseFunc(image, streams, methods, typeStart + 1, typeStart + 1 + typeSize) != typeStart + 1 + typeSize)
                return 0;

            uint name = Index(image, at + (uint)parentWidth, strings);
            uint sig = Index(image, at + (uint)(parentWidth + strings), blobs);
            if (!Fits(name, 6, (uint)streams[3]) || !Fits(sig, 5, (uint)streams[1])) return 0;
            uint n = (uint)streams[2] + name, b = (uint)streams[0] + sig;
            if (U32(image, n) == 0x6f74632e && U16(image, n + 4) == 0x72 &&
                Fits(sig, 6, (uint)streams[1]) && U32(image, b) == 0x01022005 && U16(image, b + 4) == 0x181c)
                return Construct;
            if (Fits(name, 7, (uint)streams[3]) && U32(image, n) == 0x6f766e49 &&
                U16(image, n + 4) == 0x656b && image[n + 6] == 0 &&
                U32(image, b) == 0x13002004 && image[b + 4] == 0)
                return Invoke;
            return 0;
        }

        // Accept the exact ECMA signature GENERICINST CLASS System.Func`1<I4>.
        // Returns the byte immediately after the signature or -1.
        internal static int ParseFunc(byte[] image, int[] streams, int[] methods, int start, int end)
        {
            if (end - start < 5 || image[start] != 0x15 || image[start + 1] != 0x12)
                return -1;
            var next = new int[1];
            int coded = CompressedUnsigned(image, start + 2, end, next);
            int args = next[0];
            if (coded <= 0 || (coded & 3) != 1 || end - args < 2 ||
                image[args] != 1 || image[args + 1] != 8)
                return -1;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            int row = coded >> 2;
            if (first < 0 || row < 1 || row > counts[1]) return -1;
            int strings = methods[2];
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guid);
            int scope = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            uint at = (uint)(refs + (row - 1) * (scope + 2 * strings));
            uint resolution = Index(image, at, scope);
            if ((resolution & 3) != 2 || resolution >> 2 == 0 || resolution >> 2 > (uint)counts[35]) return -1;
            uint name = Index(image, at + (uint)scope, strings);
            uint ns = Index(image, at + (uint)(scope + strings), strings);
            if (!Fits(name, 7, (uint)streams[3]) || !Fits(ns, 7, (uint)streams[3])) return -1;
            uint n = (uint)streams[2] + name, s = (uint)streams[2] + ns;
            if (U32(image, n) != 0x636e7546 || U16(image, n + 4) != 0x3160 || image[n + 6] != 0 ||
                U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0 ||
                !PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, (int)(resolution >> 2)))
                return -1;
            return args + 2;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
