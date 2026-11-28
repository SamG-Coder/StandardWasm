using System.Threading;

namespace ClosedStatics
{
    public sealed class CellValue
    {
        public readonly int Tag;
        public CellValue(int tag) { Tag = tag; }
    }

    public static class Slots<T>
    {
        public static T Value;
    }

    public static class Cache<T>
    {
        public static volatile CellValue Value = new CellValue(17);
    }

    public static class Probe
    {
        private static void Assign(ref CellValue slot, CellValue value) { slot = value; }

        public static int Independent()
        {
            var left = new CellValue(31);
            var right = new CellValue(41);
            Slots<CellValue>.Value = left;
            Slots<object>.Value = right;
            Assign(ref Slots<CellValue>.Value, right);
            Slots<object>.Value = left;
            return Slots<CellValue>.Value.Tag == 41 && ((CellValue)Slots<object>.Value).Tag == 31 ? 981 : 0;
        }

        public static int MixedRepresentations()
        {
            var original = new CellValue(53);
            Slots<int>.Value = 61;
            Slots<CellValue>.Value = original;
            Assign(ref Slots<CellValue>.Value, null);
            return Slots<int>.Value == 61 && Slots<CellValue>.Value == null ? 983 : 0;
        }

        public static int Atomic()
        {
            var first = Cache<int>.Value;
            var other = Cache<string>.Value;
            var replacement = new CellValue(71);
            // Interlocked supplies the ordering on the addressed volatile field.
#pragma warning disable CS0420
            var previous = Interlocked.CompareExchange(ref Cache<int>.Value, replacement, first);
            var failed = Interlocked.CompareExchange(ref Cache<int>.Value, other, first);
#pragma warning restore CS0420
            return previous.Tag == 17 && failed.Tag == 71 && Cache<int>.Value.Tag == 71 &&
                Cache<string>.Value.Tag == 17 && (object)first != (object)other ? 991 : 0;
        }

        public static int ScalarAddressDiagnostic() => Interlocked.Increment(ref Slots<int>.Value);
    }
}
