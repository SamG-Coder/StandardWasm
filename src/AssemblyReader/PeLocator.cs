using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class PeLocator
    {
        private const int Invalid = 1, Unsupported = 2, NoCli = 3;

        public static int LocateMetadata(byte[] image, int[] location)
        {
            if (location.Length < 2)
                return Invalid;
            if (WebcilLocator.HasSignature(image))
                return WebcilLocator.LocateMetadata(image, location);
            int peStart = ReadPeStart(image);
            if (peStart < 0)
                return Invalid;
            uint pe = (uint)peStart;
            int cli = LocatePeCli(image, pe);
            if (cli < 0)
                return -cli;
            uint optional = pe + 24;
            uint headerSize = U32(image, optional + 60);
            uint sections = optional + U16(image, pe + 20);
            uint sectionCount = U16(image, pe + 6);

            uint metadataRva = U32(image, (uint)cli + 8);
            uint metadataSize = U32(image, (uint)cli + 12);
            if (metadataRva == 0 || metadataSize < 4)
                return Invalid;
            if (metadataRva < headerSize)
                return Unsupported;
            int metadata = MapRange(image, sections + 8, sectionCount, 40, 0, metadataRva, metadataSize);
            if (metadata < 0 || U32(image, (uint)metadata) != 0x424a5342)
                return Invalid;

            // Publish only after both ranges and the metadata signature have been validated.
            location[0] = metadata;
            location[1] = (int)metadataSize;
            return 0;
        }

        // A resource directory is a file-backed range in the CLI image,
        // separate from the metadata tables that name its individual entries.
        internal static int LocateResources(byte[] image, int[] location)
        {
            int cli;
            if (WebcilLocator.HasSignature(image))
                cli = WebcilLocator.LocateCli(image);
            else
            {
                int pe = ReadPeStart(image);
                if (pe < 0) return -1;
                cli = LocatePeCli(image, (uint)pe);
            }
            if (cli < 0) return -1;
            uint rva = U32(image, (uint)cli + 24);
            uint size = U32(image, (uint)cli + 28);
            if (rva == 0 && size == 0) return -4;
            if (rva == 0 || size < 4) return -1;
            int start = MapImageRange(image, rva, size);
            if (start < 0) return -1;
            location[0] = start;
            location[1] = (int)size;
            return 0;
        }

        // Negative values carry the public LocateMetadata status.
        private static int LocatePeCli(byte[] image, uint pe)
        {
            uint sectionCount = U16(image, pe + 6);
            uint optionalSize = U16(image, pe + 20);
            uint optional = pe + 24;
            uint sections = optional + optionalSize;

            uint directory;
            uint magic = U16(image, optional);
            if (magic == 0x10b)
                directory = 96;
            else if (magic == 0x20b)
                directory = 112;
            else
                return -Unsupported;
            if (optionalSize < directory)
                return -Invalid;
            if (U32(image, optional + directory - 4) <= 14)
                return -NoCli;
            if (optionalSize < directory + 15 * 8)
                return -Invalid;

            uint cliEntry = optional + directory + 14 * 8;
            uint cliRva = U32(image, cliEntry);
            uint cliSize = U32(image, cliEntry + 4);
            if (cliRva == 0 && cliSize == 0)
                return -NoCli;
            if (cliRva == 0 || cliSize < 72)
                return -Invalid;
            uint headerSize = U32(image, optional + 60);
            if (cliRva < headerSize)
                return -Unsupported;
            int cli = MapRange(image, sections + 8, sectionCount, 40, 0, cliRva, cliSize);
            if (cli < 0)
                return -Invalid;
            uint declaredCliHeaderSize = U32(image, (uint)cli);
            if (declaredCliHeaderSize < 72 || declaredCliHeaderSize > cliSize)
                return -Invalid;
            return cli;
        }

        internal static int MapImageRange(byte[] image, uint rva, uint size)
        {
            if (WebcilLocator.HasSignature(image))
                return WebcilLocator.MapImageRange(image, rva, size);
            int peStart = ReadPeStart(image);
            if (peStart < 0)
                return -1;
            uint pe = (uint)peStart;
            uint count = U16(image, pe + 6);
            uint optionalSize = U16(image, pe + 20);
            uint optional = pe + 24;
            uint magic = U16(image, optional);
            if (optionalSize < 64 || (magic != 0x10b && magic != 0x20b) ||
                rva < U32(image, optional + 60))
                return -1;
            return MapRange(image, optional + optionalSize + 8, count, 40, 0, rva, size);
        }

        private static int ReadPeStart(byte[] image)
        {
            uint limit = (uint)image.Length;
            if (limit < 64 || U16(image, 0) != 0x5a4d)
                return -1;
            uint pe = U32(image, 60);
            if (!Fits(pe, 24, limit) || U32(image, pe) != 0x4550)
                return -1;
            uint optional = pe + 24;
            uint optionalSize = U16(image, pe + 20);
            uint sectionCount = U16(image, pe + 6);
            if (optionalSize < 2 || !Fits(optional, optionalSize, limit) ||
                !Fits(optional + optionalSize, sectionCount * 40, limit))
                return -1;
            return (int)pe;
        }

        internal static int MapRange(byte[] image, uint sections, uint count, uint stride, uint minimumRawOffset, uint rva, uint size)
        {
            // A requested image range must stay inside the 32-bit RVA space.
            if (size == 0 || size - 1 > uint.MaxValue - rva)
                return -1;

            int result = -1;
            for (uint index = 0; index < count; index++)
            {
                uint section = sections + index * stride;
                uint virtualSize = U32(image, section);
                uint virtualAddress = U32(image, section + 4);
                uint rawSize = U32(image, section + 8);
                uint rawOffset = U32(image, section + 12);
                if (rva < virtualAddress || rva - virtualAddress >= virtualSize)
                    continue;
                if (result >= 0)
                    return -1;
                uint delta = rva - virtualAddress;
                // The section can have a virtual tail. Only its file-backed bytes are readable.
                if (rawOffset < minimumRawOffset || !Fits(delta, size, virtualSize) || !Fits(delta, size, rawSize) || !Fits(rawOffset, rawSize, (uint)image.Length))
                    return -1;
                result = (int)(rawOffset + delta);
            }
            return result;
        }
    }
}
