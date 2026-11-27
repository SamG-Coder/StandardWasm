using System.Collections.Generic;

namespace ProvenInterfaceCasts
{
    public sealed class TextComparer : IEqualityComparer<string>
    {
        public bool Equals(string left, string right) => left == right;
        public int GetHashCode(string value) => value.Length;
    }

    public static class Probe
    {
        private static IEqualityComparer<T> Cast<T>(IEqualityComparer<string> value) =>
            (IEqualityComparer<T>)(object)value;

        private static IEqualityComparer<T> CastUnknown<T>(object value) =>
            (IEqualityComparer<T>)value;

        public static int Identity()
        {
            IEqualityComparer<string> comparer = new TextComparer();
            return (object)Cast<string>(comparer) == (object)comparer ? 967 : 0;
        }

        private static IEqualityComparer<string> casts(IEqualityComparer<string> value) => Cast<string>(value);

        public static int Null() => (object)Cast<string>(null) == null ? 971 : 0;

        public static int UnknownDiagnostic()
        {
            object comparer = new TextComparer();
            return (object)CastUnknown<string>(comparer) == comparer ? 977 : 0;
        }
    }
}
