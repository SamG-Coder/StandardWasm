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

        private struct Payload
        {
            public string Text;
            public int Count;
        }

        private struct Constructed
        {
            public readonly string Tag;
            public readonly Payload Data;

            public Constructed(string tag)
            {
                Tag = tag;
                Data = default;
            }
        }

        private static void ClearKey<T>(ref Entry<T> item) => item.Key = default;

        public static int ClearInteriorFields(int input)
        {
            var entries = new Entry<Payload>[2];
            ref Entry<Payload> first = ref entries[0];
            ref Entry<Payload> alias = ref entries[0];
            first.Count = input;
            first.Key = new Payload { Text = "first", Count = 7 };
            entries[1].Key = new Payload { Text = "second", Count = 11 };
            Entry<Payload> copy = first;
            ClearKey(ref alias);

            var references = new Entry<string>[1];
            references[0].Key = "original";
            ClearKey(ref references[0]);
            var numbers = new Entry<long>[1];
            numbers[0].Key = 1L << 40;
            ClearKey(ref numbers[0]);
            int observed = first.Key.Count + entries[1].Key.Count + copy.Key.Count;
            if (alias.Key.Text == null) observed += 13;
            if (copy.Key.Text != null) observed += 17;
            if (references[0].Key == null) observed += 19;
            if (numbers[0].Key == 0) observed += 23;
            var initialized = new Constructed("tag");
            if (initialized.Tag != null && initialized.Data.Text == null) observed += 29;
            return unchecked(first.Count * 3 + observed);
        }

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
