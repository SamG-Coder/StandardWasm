using System;

namespace ValueConstructionDemo
{
    public static class EntryArrayProbe
    {
        private struct Entry<T>
        {
            public T Key;
            public int Count;
        }

        public static int UpdateEntryArray(int input)
        {
            var entries = new Entry<int>[2];
            ref Entry<int> first = ref entries[0];
            ref Entry<int> second = ref entries[0];
            int zero = entries[1].Count;
            entries[0] = new Entry<int> { Key = 13, Count = input };
            Entry<int> copy = entries[0];
            IncrementEntry(ref second);
            return unchecked(first.Count * 7 + entries[0].Count + copy.Count * 3 + first.Key + zero);
        }

        private static void IncrementEntry(ref Entry<int> item) => item.Count = unchecked(item.Count + 4);

        public static int CheckEntryArrayFailure(int index)
        {
            Entry<int>[] entries = null;
            if (index != int.MinValue) entries = new Entry<int>[1];
            try { return entries[index].Count; }
            catch (NullReferenceException) { return -7; }
            catch (IndexOutOfRangeException) { return -11; }
        }
    }
}
