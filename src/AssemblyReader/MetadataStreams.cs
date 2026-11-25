using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    public static class MetadataStreams
    {
        public const int RecordWidth = 4;
        public const int Invalid = -1;
        public const int OutputTooSmall = -2;

        // One record per stream: absolute name offset, name length, absolute payload offset, payload length.
        // The output catalog is published only after every record is validated.
        public static int Read(byte[] image, int root, int size, int[] catalog)
        {
            if (root < 0 || size < 20 || !Fits((uint)root, (uint)size, (uint)image.Length) ||
                U32(image, (uint)root) != 0x424a5342)
                return Invalid;
            uint end = (uint)(root + size);
            uint version = (uint)root + 16;
            uint versionSize = U32(image, (uint)root + 12);
            if (versionSize == 0 || (versionSize & 3) != 0 || !Fits(version, versionSize, end))
                return Invalid;
            uint header = version + versionSize;
            if (image[header - 1] != 0 || !Fits(header, 4, end))
                return Invalid;
            int count = (int)U16(image, header + 2);
            uint cursor = header + 4;
            // Each stream header needs eight fixed bytes and at least four name bytes.
            if (!Fits(cursor, (uint)count * 12, end))
                return Invalid;
            if (count > catalog.Length / RecordWidth)
                return OutputTooSmall;

            var records = new int[count * RecordWidth];
            for (int i = 0; i < count; i++)
            {
                if (!Fits(cursor, 12, end))
                    return Invalid;
                uint offset = U32(image, cursor);
                uint length = U32(image, cursor + 4);
                if (!Fits(offset, length, (uint)size))
                    return Invalid;

                uint name = cursor + 8;
                int nameLength = ReadName(image, name, end);
                if (nameLength <= 0)
                    return Invalid;
                uint paddedLength = (uint)((nameLength + 4) & ~3);
                if (!Fits(name, paddedLength, end))
                    return Invalid;
                for (uint pad = (uint)nameLength; pad < paddedLength; pad++)
                    if (image[name + pad] != 0)
                        return Invalid;

                int slot = i * RecordWidth;
                records[slot] = (int)name;
                records[slot + 1] = nameLength;
                records[slot + 2] = root + (int)offset;
                records[slot + 3] = (int)length;
                cursor = name + paddedLength;
            }

            for (int i = 0; i < records.Length; i += RecordWidth)
            {
                int start = records[i + 2], length = records[i + 3];
                if (start < (int)cursor)
                    return Invalid;
                for (int j = 0; j < i; j += RecordWidth)
                {
                    int priorStart = records[j + 2], priorLength = records[j + 3];
                    if (SameName(image, records[i], records[i + 1], records[j], records[j + 1]) ||
                        (length != 0 && priorLength != 0 && start < priorStart + priorLength && priorStart < start + length))
                        return Invalid;
                }
            }

            for (int i = 0; i < records.Length; i++)
                catalog[i] = records[i];
            return count;
        }

        private static int ReadName(byte[] image, uint start, uint end)
        {
            for (uint i = 0; i < 32 && i < end - start; i++)
            {
                byte value = image[start + i];
                if (value == 0)
                    return (int)i;
                if (value < 32 || value > 126)
                    return -1;
            }
            return -1;
        }

        private static bool SameName(byte[] image, int left, int leftLength, int right, int rightLength)
        {
            if (leftLength != rightLength)
                return false;
            for (int i = 0; i < leftLength; i++)
                if (image[left + i] != image[right + i])
                    return false;
            return true;
        }
    }
}
