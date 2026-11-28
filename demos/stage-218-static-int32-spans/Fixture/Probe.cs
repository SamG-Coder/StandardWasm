using System;
using System.Runtime.CompilerServices;

namespace StaticInt32Spans
{
    public static class Probe
    {
        // Roslyn emits ldtoken FieldRVA followed by RuntimeHelpers.CreateSpan<int>.
        private static ReadOnlySpan<int> Table => [35, -7, int.MinValue, 0x10203040, int.MaxValue];

        public static int Length() => Table.Length;
        public static int First() => Table[0];
        public static int NegativeValue() => Table[1];
        public static int MinValue() => Table[2];
        public static int Last() => Table[4];
        public static int Sum() => SumCore(Table);
        public static int NegativeIndex() => ReadWithBounds(-1);
        public static int EndIndex() => ReadWithBounds(5);
        public static int WideIndex() => ReadWithBounds(int.MaxValue);
        public static int MiddleIndex() => ReadWithBounds(3);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SumCore(ReadOnlySpan<int> view)
        {
            int sum = 0;
            for (int i = 0; i < view.Length; i++) sum = unchecked(sum + view[i]);
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadWithBounds(int index)
        {
            try
            {
                ReadOnlySpan<int> values = Table;
                return values[index];
            }
            catch (IndexOutOfRangeException)
            {
                return -101;
            }
        }
    }
}
