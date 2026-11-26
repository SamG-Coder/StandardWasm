using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class PinnedStringIl
    {
        private const int AfterPin = 16;

        // Prove the compiler-generated fixed-string prefix and a read-only,
        // non-escaping consumer. A general pinned local still needs a proof
        // for every use, including calls and exceptional exits.
        internal static bool Matches(byte[] image, int[] heaps, int[] layout, int[] body, int localBlob)
        {
            if (body[1] != 18 || body[5] != 0 || localBlob < 1 || localBlob > heaps[1] - 6) return false;
            uint sig = (uint)(heaps[0] + localBlob);
            if (U32(image, sig) != 0x45010705 || U16(image, sig + 4) != 0x0310) return false;
            uint at = (uint)body[0];
            if (U32(image, at) != 0x16042d02 || U32(image, at + 4) != 0x02092be0 ||
                image[at + 8] != 0x28 || image[at + 13] != 0x0a ||
                U16(image, at + 14) != 0xe006 || U16(image, at + AfterPin) != 0x2a49) return false;
            uint call = U32(image, at + 9);
            return call >> 24 == 0x0a &&
                StringMetadata.Member(image, heaps, layout, (int)(call & 0xffffff)) == StringMetadata.PinnableReference;
        }

        internal static int Decode(byte[] image, int[] body, int[] records, int[] location)
        {
            records[0] = PinString;
            records[1] = 0; // the sole string parameter
            records[2] = AfterPin;
            return BranchIl.Decode(image, body[0], body[1], 1, 0, records, location, false, AfterPin);
        }
    }
}
