using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MethodBodies
    {
        internal const int Malformed = -1, Unsupported = -2, Missing = -3;

        // layout contains file offset, byte count, max stack, local signature token,
        // init-locals flag and extra-sections flag. Publish only after range validation.
        internal static int Read(byte[] image, uint rva, int[] layout)
        {
            if (rva == 0)
                return Missing;
            if (layout.Length < 6)
                return Malformed;
            int fileStart = PeLocator.MapImageRange(image, rva, 1);
            if (fileStart < 0)
                return Malformed;
            uint start = (uint)fileStart;
            uint header = image[start];
            uint format = header & 3;
            uint headerSize, codeSize;
            int maxStack, token = 0, initLocals = 0, extraSections = 0;
            if (format == 2)
            {
                headerSize = 1;
                codeSize = header >> 2;
                maxStack = 8;
            }
            else if (format == 3)
            {
                // A fat header starts on a four-byte boundary and has three mandatory words.
                if ((rva & 3) != 0 || PeLocator.MapImageRange(image, rva, 12) < 0)
                    return Malformed;
                uint flags = U16(image, start);
                headerSize = (flags >> 12) * 4;
                if (headerSize < 12)
                    return Malformed;
                if ((flags & 0x0fe4u) != 0)
                    return Unsupported;
                codeSize = U32(image, start + 4);
                maxStack = (int)U16(image, start + 2);
                token = unchecked((int)U32(image, start + 8));
                initLocals = (int)((flags >> 4) & 1);
                extraSections = (int)((flags >> 3) & 1);
            }
            else
            {
                return Unsupported;
            }

            if (codeSize > uint.MaxValue - headerSize ||
                PeLocator.MapImageRange(image, rva, headerSize + codeSize) < 0)
                return Malformed;
            layout[0] = (int)(start + headerSize);
            layout[1] = (int)codeSize;
            layout[2] = maxStack;
            layout[3] = token;
            layout[4] = initLocals;
            layout[5] = extraSections;
            return 0;
        }
    }
}
