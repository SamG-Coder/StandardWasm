using System;

namespace ObsoleteStaticFields
{
    public static class Probe
    {
        private static int initializations;
        [Obsolete("The external contract keeps this field for old binaries.")]
        private static readonly char[] Letters;
        [Obsolete]
        private static int Counter;

#pragma warning disable CS0612, CS0618 // This fixture exercises already-compiled deprecated access.
        static Probe()
        {
            initializations++;
            Letters = new char[3];
            Letters[0] = 'a';
            Letters[1] = '\u03bb';
            Letters[2] = 'z';
            Counter = 27;
        }

        public static int ReadInitializer() => Letters.Length * 10000 + Letters[1] + initializations;

        public static int PreserveArrayAlias()
        {
            char[] alias = Letters;
            alias[2] = 'v';
            return ReferenceEquals(alias, Letters) ? Letters[2] : -1;
        }

        public static int UpdateScalar()
        {
            Counter += 6;
            return 100 * initializations + Counter;
        }
#pragma warning restore CS0612, CS0618

        public static int RejectOtherAttribute() => Unrelated.Value;

        private static class Unrelated
        {
            [Other.Obsolete]
            internal static readonly int Value = 47;
        }
    }
}

namespace ObsoleteStaticFields.Other
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ObsoleteAttribute : Attribute { }
}
