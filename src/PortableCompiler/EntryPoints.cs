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
            var next = new int[1];
            while (position < entries.Length)
            {
                if (entries.Length - position < 9 || count >= offsets.Length)
                    return -1;
                uint length = ImageBytes.U32(entries, (uint)position + 4);
                int name = position + 8;
                if (length == 0 || length > (uint)(entries.Length - name))
                    return -1;
                if (!IsExportName(entries, name, (int)length, next)) return -2;
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

        private static bool IsExportName(byte[] bytes, int start, int length, int[] next)
        {
            int end = start + length;
            while (start < end)
            {
                int scalar = Utf8Scalars.Read(bytes, start, end, next);
                // ASCII controls include the NUL prefix reserved for internal exports.
                if (scalar < 32 || scalar == 127) return false;
                start = next[0];
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
