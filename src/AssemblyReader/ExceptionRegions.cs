using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExceptionRegions
    {
        internal const int Width = 6;
        internal const int Malformed = -1, Unsupported = -2, Capacity = -3;

        // Each record is flags, try start/length, handler start/length, and the
        // catch token or filter offset. The code layout is from MethodBodies.Read.
        internal static int Read(byte[] image, uint methodRva, int[] body, int[] clauses)
        {
            int count = Count(image, methodRva, body);
            if (count <= 0) return count;
            if (count > clauses.Length / Width) return Capacity;
            // The first pass validates the entire chain before any publication.
            return Walk(image, methodRva, body, clauses, true);
        }

        internal static int Count(byte[] image, uint methodRva, int[] body) =>
            Walk(image, methodRva, body, body, false);

        private static int Walk(byte[] image, uint methodRva, int[] body, int[] clauses, bool publish)
        {
            if (body[5] == 0) return 0;
            int method = PeLocator.MapImageRange(image, methodRva, 1);
            if (method < 0) return Malformed;
            uint endOfCode = (uint)(body[0] - method) + (uint)body[1];
            if (endOfCode > uint.MaxValue - methodRva || methodRva + endOfCode > uint.MaxValue - 3)
                return Malformed;
            uint section = (methodRva + endOfCode + 3) & ~3u;
            int count = 0;
            while (true)
            {
                int header = PeLocator.MapImageRange(image, section, 4);
                if (header < 0) return Malformed;
                int kind = image[header];
                if ((kind & 0x3f) != 1) return Unsupported;
                bool fat = (kind & 0x40) != 0;
                uint bytes = fat ? U32(image, (uint)header) >> 8 : image[header + 1];
                uint width = fat ? 24u : 12u;
                uint distance = section - methodRva;
                if ((!fat && U16(image, (uint)header + 2) != 0) || bytes < 4 ||
                    (bytes - 4) % width != 0 || bytes > uint.MaxValue - distance ||
                    PeLocator.MapImageRange(image, methodRva, distance + bytes) < 0)
                    return Malformed;

                uint at = (uint)header + 4;
                uint end = (uint)header + bytes;
                for (; at < end; at += width)
                {
                    int status = ReadClause(image, at, fat, (uint)body[1], clauses, count * Width, publish);
                    if (status < 0) return status;
                    count++;
                }
                if ((kind & 0x80) == 0) return count;
                // The EH table formats have four-byte-aligned total sizes.
                if (bytes > uint.MaxValue - section) return Malformed;
                section += bytes;
            }
        }

        private static int ReadClause(byte[] image, uint at, bool fat, uint codeSize, int[] clauses, int slot, bool publish)
        {
            uint flags = fat ? U32(image, at) : U16(image, at);
            uint tryStart = fat ? U32(image, at + 4) : U16(image, at + 2);
            uint tryLength = fat ? U32(image, at + 8) : image[at + 4];
            uint handler = fat ? U32(image, at + 12) : U16(image, at + 5);
            uint handlerLength = fat ? U32(image, at + 16) : image[at + 7];
            uint data = U32(image, at + (fat ? 20u : 8u));
            if (tryLength == 0 || handlerLength == 0 || !Fits(tryStart, tryLength, codeSize) ||
                !Fits(handler, handlerLength, codeSize) ||
                (tryStart < handler + handlerLength && handler < tryStart + tryLength))
                return Malformed;
            if (flags == 0)
            {
                uint table = data >> 24;
                if ((data & 0xffffff) == 0 || (table != 1 && table != 2 && table != 27)) return Malformed;
            }
            else if (flags == 1)
            {
                if (data >= handler || (data < tryStart + tryLength && tryStart < handler)) return Malformed;
            }
            else if (flags != 2 && flags != 4)
                return Unsupported;
            else
                data = 0; // Finally and fault do not use the union field.

            if (publish)
            {
                clauses[slot] = (int)flags;
                clauses[slot + 1] = (int)tryStart;
                clauses[slot + 2] = (int)tryLength;
                clauses[slot + 3] = (int)handler;
                clauses[slot + 4] = (int)handlerLength;
                clauses[slot + 5] = (int)data;
            }
            return 0;
        }
    }
}
