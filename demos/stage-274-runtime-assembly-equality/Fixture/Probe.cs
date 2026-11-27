using System.Reflection;

namespace RuntimeAssemblyEquality
{
    internal sealed class SyntheticAssembly : Assembly
    {
        public override bool Equals(object other) => other is SyntheticAssembly;

        public override int GetHashCode() => 1;
    }

    public static class Probe
    {
        private static bool Equal(Assembly first, Assembly second) => first == second;

        private static bool Different(Assembly first, Assembly second) => first != second;

        public static int Same()
        {
            Assembly first = Assembly.GetExecutingAssembly();
            Assembly second = typeof(Probe).Module.Assembly;
            return Equal(first, second) && !Different(first, second) ? 157 : 0;
        }

        public static int Distinct()
        {
            Assembly first = typeof(Probe).Assembly;
            Assembly second = typeof(int).Assembly;
            return !Equal(first, second) && Different(second, first) ? 163 : 0;
        }

        public static int NullOperands()
        {
            Assembly value = typeof(Probe).Assembly;
            return Equal(null, null) && !Different(null, null) && !Equal(value, null) &&
                !Equal(null, value) && Different(value, null) && Different(null, value) ? 167 : 0;
        }

        public static int OverrideBoundary()
        {
            Assembly first = new SyntheticAssembly();
            Assembly second = new SyntheticAssembly();
            return !ReferenceEquals(first, second) && Equal(first, second) ? 173 : 0;
        }
    }
}
