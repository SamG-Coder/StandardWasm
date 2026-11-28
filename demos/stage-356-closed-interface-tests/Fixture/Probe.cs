using System;
using System.Collections.Generic;

namespace ClosedInterfaceTests
{
    public sealed class ObjectComparer : IEqualityComparer<object>
    {
        bool IEqualityComparer<object>.Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => 41;
    }

    public sealed class TextComparer : IEqualityComparer<string>
    {
        public bool Equals(string left, string right) => left == right;
        public int GetHashCode(string value) => value.Length;
    }

    public static class Probe
    {
        private enum Number { Twelve = 12 }

        private static IEqualityComparer<object> Cast(object value) => (IEqualityComparer<object>)value;
        private static IEqualityComparer<object> Test(object value) => value as IEqualityComparer<object>;

        public static int CastPreservesIdentity()
        {
            object original = new ObjectComparer();
            return ReferenceEquals(original, Cast(original)) ? 1 : 0;
        }

        public static int CallsOriginalImplementation()
        {
            object original = new ObjectComparer();
            var comparer = Cast(original);
            return comparer.Equals(original, original) && !comparer.Equals(original, null)
                ? comparer.GetHashCode(original) : 0;
        }

        public static int IsInstPreservesIdentity()
        {
            object original = new ObjectComparer();
            return ReferenceEquals(original, Test(original)) ? 1 : 0;
        }

        public static int RejectsDifferentConstruction() => Test(new TextComparer()) == null ? 1 : 0;

        public static int NullIsPreserved() => Cast(null) == null && Test(null) == null ? 1 : 0;

        public static int ThrowsManagedInvalidCast()
        {
            try
            {
                Cast(new TextComparer());
                return 0;
            }
            catch (InvalidCastException)
            {
                return 1;
            }
        }

        private static IEquatable<int> TestBox(object value) => value as IEquatable<int>;

        public static int PrimitiveBox()
        {
            object boxed = 12;
            return ReferenceEquals(boxed, TestBox(boxed)) && TestBox(12L) == null && TestBox(Number.Twelve) == null ? 1 : 0;
        }

        private static IEquatable<string> TestString(object value) => value as IEquatable<string>;
        private static bool SameObject(object left, object right) => ReferenceEquals(left, right);

        public static int RuntimeString()
        {
            object value = "sample";
            return SameObject(value, TestString(value)) && TestString(new ObjectComparer()) == null ? 1 : 0;
        }
    }
}
