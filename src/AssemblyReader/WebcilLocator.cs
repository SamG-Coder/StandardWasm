using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class WebcilLocator
    {
        private const uint HeaderSize = 28;
        private const uint SectionSize = 16;

        internal static bool HasSignature(byte[] image) =>
            image.Length >= 4 && U32(image, 0) == 0x4c496257; // WbIL

        internal static int LocateMetadata(byte[] image, int[] location)
        {
            int cli = LocateCli(image);
            if (cli < 0)
                return 1;
            uint rva = U32(image, (uint)cli + 8);
            uint size = U32(image, (uint)cli + 12);
            int metadata = MapImageRange(image, rva, size);
            if (size < 4 || metadata < 0 || U32(image, (uint)metadata) != 0x424a5342)
                return 1;
            location[0] = metadata;
            location[1] = (int)size;
            return 0;
        }

        internal static int ReadEntryToken(byte[] image)
        {
            int cli = LocateCli(image);
            if (cli < 0)
                return -1;
            uint flags = U32(image, (uint)cli + 16);
            uint token = U32(image, (uint)cli + 20);
            if ((flags & 0x10) != 0 || token <= 0x06000000 || token > 0x06ffffff)
                return -1;

            return (int)token;
        }

        internal static int MapImageRange(byte[] image, uint rva, uint size)
        {
            if ((uint)image.Length < HeaderSize || U32(image, 4) != 0 || U16(image, 10) != 0)
                return -1;
            uint count = U16(image, 8);
            uint tableEnd = HeaderSize + count * SectionSize;
            if (tableEnd > (uint)image.Length)
                return -1;
            // Webcil stores the same four mapping fields as PE at the start of each section row.
            return PeLocator.MapRange(image, HeaderSize, count, SectionSize, tableEnd, rva, size);
        }

        private static int LocateCli(byte[] image)
        {
            if ((uint)image.Length < HeaderSize || !HasSignature(image))
                return -1;
            uint rva = U32(image, 12);
            uint size = U32(image, 16);
            if (rva == 0 || size < 72)
                return -1;
            int cli = MapImageRange(image, rva, size);
            if (cli < 0)
                return -1;
            uint declared = U32(image, (uint)cli);
            return declared >= 72 && declared <= size ? cli : -1;
        }
    }
}
