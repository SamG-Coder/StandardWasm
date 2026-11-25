namespace PeLibrary
{
    internal static class MethodNames
    {
        // This demo lookup is scoped to a unique name across the module.
        // Production overload resolution will also need the owner and signature.
        internal static int FindRow(byte[] image, byte[] name)
        {
            var layout = new int[4];
            var heaps = new int[4];
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
