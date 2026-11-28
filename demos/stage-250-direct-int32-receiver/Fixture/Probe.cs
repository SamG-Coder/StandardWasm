using System;

namespace Int32Receiver
{
    public sealed class Counter
    {
        public int Value;

        public Counter(int value) => Value = value;
    }

    public static class Probe
    {
        private static int Compare(int left, int right) => left.CompareTo(right);

        public static int SignedExtremes()
        {
            return Compare(int.MinValue, int.MaxValue) < 0 && Compare(int.MaxValue, int.MinValue) > 0 &&
                Compare(0, 0) == 0 ? 1201 : 0;
        }

        public static int DifferentInstanceMethods()
        {
            int value = -17;
            return value.Equals(-17) && !value.Equals(17) && value.GetHashCode() == -17 ? 1213 : 0;
        }

        private static int ChangeAndCompare(ref int alias, Counter receiver)
        {
            receiver.Value = -43;
            return alias.CompareTo(-41);
        }

        public static int LiveClassField()
        {
            var receiver = new Counter(53);
            return ChangeAndCompare(ref receiver.Value, receiver) < 0 && receiver.Value == -43 ? 1217 : 0;
        }

        private static int CompareElement(ref int element, int other) => element.CompareTo(other);

        public static int ArrayElement()
        {
            var values = new[] { 47, -29 };
            return CompareElement(ref values[0], 41) > 0 &&
                CompareElement(ref values[1], -29) == 0 && values[0] == 47 ? 1223 : 0;
        }

        public static int BoxedAndDirect()
        {
            int value = 59;
            object boxed = value;
            value = -61;
            return (int)boxed == 59 && value.CompareTo(-53) < 0 ? 1229 : 0;
        }
    }
}
