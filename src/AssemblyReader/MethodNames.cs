namespace PeLibrary
{
    internal static class MethodNames
    {
        internal static bool HasInitializer(byte[] image, int[] layout, int[] heaps, int count)
        {
            for (int row = 1; row <= count; row++)
            {
                int offset = MethodDefinitions.ReadColumn(image, layout, row, 3);
                if (offset <= 0 || offset > heaps[3] - 7)
                    continue;
                int start = heaps[2] + offset;
                // ".cctor" followed by the heap terminator.
                if (ImageBytes.U32(image, (uint)start) == 0x7463632e &&
                    ImageBytes.U16(image, (uint)start + 4) == 0x726f && image[start + 6] == 0)
                    return true;
            }
            return false;
        }

        // This demo lookup is scoped to a unique name across the module.
        // Production overload resolution will also need the owner and signature.
        internal static int FindRow(byte[] image, byte[] name)
        {
            var layout = new int[4];
            var heaps = new int[8];
            int count = CompressedTables.FindWithHeaps(image, layout, heaps);
            int row = 0;
            for (int i = 1; i <= count; i++)
            {
                int offset = MethodDefinitions.ReadColumn(image, layout, i, 3);
                if (offset <= 0 || offset >= heaps[3] || name.Length >= heaps[3] - offset)
                    continue;
                int start = heaps[2] + offset;
                if (image[start + name.Length] != 0)
                    continue;
                int j = 0;
                for (; j < name.Length; j++)
                    if (image[start + j] != name[j])
                        break;
                if (j == name.Length)
                {
                    if (row != 0)
                        return -1;
                    row = i;
                }
            }
            return row == 0 ? -1 : row;
        }
    }
}
