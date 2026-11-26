using PeLibrary;

namespace PortableCompiler
{
    internal static class EntryPoints
    {
        // Little-endian records: MethodDef row, name byte count, UTF-8 name.
        // Offsets are written only after each individual descriptor is bounded.
        internal static int Read(byte[] entries, int[] offsets)
        {
            int position = 0, count = 0;
            while (position < entries.Length)
            {
                if (entries.Length - position < 9 || count >= offsets.Length)
                    return -1;
                uint length = ImageBytes.U32(entries, (uint)position + 4);
                int name = position + 8;
                if (length == 0 || length > (uint)(entries.Length - name))
                    return -1;
                if (!IsExportName(entries, name, (int)length)) return -2;
                for (int i = 0; i < count; i++)
                {
                    int previous = offsets[i];
                    if (ImageBytes.U32(entries, (uint)previous + 4) == length &&
                        SameName(entries, name, previous + 8, (int)length))
                        return -1;
                }
                offsets[count++] = position;
                position = name + (int)length;
            }
            return count == 0 ? -1 : count;
        }

        private static bool IsExportName(byte[] bytes, int start, int length)
        {
            int end = start + length;
            while (start < end)
            {
                int first = bytes[start++];
                // ASCII controls include the NUL prefix reserved for internal exports.
                if (first < 128)
                {
                    if (first < 32 || first == 127) return false;
                    continue;
                }

                if (first < 194 || first > 244) return false;
                int remaining = first < 224 ? 1 : first < 240 ? 2 : 3;
                if (remaining > end - start) return false;

                // Disallow overlong encodings, surrogate values and values above U+10FFFF.
                int low = first == 224 ? 160 : first == 240 ? 144 : 128;
                int high = first == 237 ? 159 : first == 244 ? 143 : 191;
                int second = bytes[start++];
                if (second < low || second > high) return false;
                for (int index = 1; index < remaining; index++)
                {
                    int next = bytes[start++];
                    if (next < 128 || next > 191) return false;
                }
            }
            return true;
        }

        private static bool SameName(byte[] entries, int name, int other, int length)
        {
            for (int i = 0; i < length; i++)
                if (entries[other + i] != entries[name + i])
                    return false;
            return true;
        }
    }
}
