using System;
using System.Collections.Generic;

namespace DefaultEqualityComparerFixture
{
    public readonly struct Bucket : IEquatable<Bucket>
    {
        private readonly int value;

        public Bucket(int value) => this.value = value;

        public bool Equals(Bucket other) => value % 10 == other.value % 10;
        public override bool Equals(object other) => other is Bucket bucket && Equals(bucket);
        public override int GetHashCode() => value % 10;
    }

    public enum Severity : ushort
    {
        Low = 1,
        High = 0x8000
    }

    public sealed class NamedValue
    {
        private readonly int value;

        public NamedValue(int value) => this.value = value;

        public override bool Equals(object other) => other is NamedValue named && value == named.value;
        public override int GetHashCode() => value;
    }

    public static class Probe
    {
        public static int Strings()
        {
            var comparer = EqualityComparer<string>.Default;
            string repeated = new string(new[] { 'x', 'x', 'x' });
            return (comparer.Equals(repeated, "xxx") ? 1 : 0)
                | (comparer.Equals(repeated, "XXX") ? 2 : 0)
                | (comparer.Equals(null, null) ? 4 : 0);
        }

        public static int EquatableValues()
        {
            var comparer = EqualityComparer<Bucket>.Default;
            return (comparer.Equals(new Bucket(12), new Bucket(22)) ? 1 : 0)
                | (comparer.Equals(new Bucket(12), new Bucket(23)) ? 2 : 0)
                | (comparer.GetHashCode(new Bucket(32)) << 2);
        }

        public static int NullableValues()
        {
            var comparer = EqualityComparer<Bucket?>.Default;
            return (comparer.Equals(null, null) ? 1 : 0)
                | (comparer.Equals(new Bucket(12), new Bucket(22)) ? 2 : 0)
                | (comparer.Equals(new Bucket(12), null) ? 4 : 0);
        }

        public static int EnumValues()
        {
            var comparer = EqualityComparer<Severity>.Default;
            return (comparer.Equals(Severity.High, (Severity)0x8000) ? 1 : 0)
                | (comparer.Equals(Severity.High, Severity.Low) ? 2 : 0)
                | (comparer.GetHashCode(Severity.High) << 2);
        }

        public static int ObjectFallback()
        {
            var comparer = EqualityComparer<NamedValue>.Default;
            return (comparer.Equals(new NamedValue(5), new NamedValue(5)) ? 1 : 0)
                | (comparer.Equals(new NamedValue(5), null) ? 2 : 0)
                | (comparer.GetHashCode(new NamedValue(8)) << 2);
        }

        public static int CachedIdentity()
        {
            var first = EqualityComparer<Bucket>.Default;
            var second = EqualityComparer<Bucket>.Default;
            object different = EqualityComparer<Severity>.Default;
            return (ReferenceEquals(first, second) ? 1 : 0)
                | (ReferenceEquals(first, different) ? 2 : 0);
        }
    }
}
