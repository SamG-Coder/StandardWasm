namespace ArrayLibrary
{
    public static class Sequences
    {
        // The returned weighted sum depends on allocation, zero initialization, reads and writes.
        public static int PrefixChecksum(int count, int seed)
        {
            var items = new int[count];
            for (int i = 1; i < items.Length; i++)
                items[i] = items[i - 1] * 31 + seed + i;
            int checksum = 0;
            for (int i = 0; i < items.Length; i++)
                checksum ^= items[i] * (i + 1);
            return checksum;
        }
    }
}
