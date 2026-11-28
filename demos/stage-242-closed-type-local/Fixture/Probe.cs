using System;
using System.Collections.Generic;

namespace ClosedTypeLocals
{
    public static class Probe
    {
        private static int Equality<T>()
        {
            Type type = typeof(T);
            if (type == typeof(string)) return 1009;
            return typeof(IEnumerable<>).IsAssignableFrom(type) ? 0 : 1007;
        }

        private static int Inequality<T>()
        {
            Type type = typeof(T);
            if (type != typeof(int)) return typeof(IEnumerable<>).IsAssignableFrom(type) ? 0 : 1007;
            return 1013;
        }

        private static int KeepLocal<T>()
        {
            Type type = typeof(T);
            if (type == typeof(string)) return type.Name.Length + 1100;
            return type.Name.Length + 1200;
        }

        public static int Equal() => Equality<string>();
        public static int Unequal() => Inequality<int>();
        public static int LiveString() => KeepLocal<string>();
        public static int LiveInt() => KeepLocal<int>();
    }
}
