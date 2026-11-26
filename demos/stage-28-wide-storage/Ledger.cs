namespace WideStorageLibrary
{
    internal sealed class Journal
    {
        internal long Balance;
        internal ulong Tag;
        internal long[] Adjustments;
        internal ulong[] Tags;
    }

    public static class Ledger
    {
        public static long Apply(long initial, int index, int size)
        {
            var journal = new Journal();
            journal.Balance = initial;
            journal.Adjustments = Allocate(size);
            long[] alias = journal.Adjustments;
            Store(alias, index, unchecked(initial * 3));
            return unchecked(journal.Balance + Read(journal.Adjustments, index));
        }

        public static ulong ApplyTag(ulong tag, int index, int size)
        {
            var journal = new Journal();
            journal.Tag = tag;
            journal.Tags = new ulong[size];
            journal.Tags[index] = tag;
            return journal.Tags[index] / 3UL ^ (journal.Tag >> 61);
        }

        public static long ReadDefault(int index, int size)
        {
            var journal = new Journal();
            journal.Adjustments = Allocate(size);
            journal.Tags = new ulong[size];
            return unchecked(journal.Balance + (long)journal.Tag +
                journal.Adjustments[index] + (long)journal.Tags[index]);
        }

        public static int Length(int size) => Allocate(size).Length;
        public static long NullRead() => Read(new Journal().Adjustments, 0);
        public static long RefUpdate(int size, int index) => Increment(ref Allocate(size)[index]);

        private static long[] Allocate(int size) => new long[size];
        private static long Read(long[] values, int index) => values[index];
        private static void Store(long[] values, int index, long value) => values[index] = value;
        private static long Increment(ref long value) => ++value;
    }
}
