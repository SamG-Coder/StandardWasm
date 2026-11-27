using System;
using System.Collections.Generic;
using System.Reflection;

[assembly: AssemblyMetadata("Duplicate", "one")]
[assembly: AssemblyMetadata("Duplicate", "é🙂")]
[assembly: AssemblyMetadata("NullValue", null)]
[assembly: AssemblyMetadata("EmptyValue", "")]

namespace AssemblyMetadataFixture
{
    public sealed class Marker { }

    public static class Probe
    {
        private static IEnumerable<AssemblyMetadataAttribute> Query(Assembly assembly) =>
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>();

        public static int DecodedFields()
        {
            var attributes = Query(typeof(Marker).Assembly).GetEnumerator();
            if (!attributes.MoveNext()) return 0;
            var first = attributes.Current;
            if (first.Key.Length != 9 || first.Key[0] != 'D' || first.Value.Length != 3 || first.Value[0] != 'o') return 0;
            if (!attributes.MoveNext()) return 0;
            var second = attributes.Current;
            if (ReferenceEquals(first, second) || second.Key.Length != 9 || second.Key[0] != 'D' ||
                second.Value.Length != 3 || second.Value[0] != 0x00e9 || second.Value[1] != 0xd83d || second.Value[2] != 0xde42)
                return 0;
            if (!attributes.MoveNext() || attributes.Current.Key[0] != 'N' || attributes.Current.Value != null) return 0;
            if (!attributes.MoveNext() || attributes.Current.Key[0] != 'E' ||
                !ReferenceEquals(attributes.Current.Value, string.Empty)) return 0;
            return attributes.MoveNext() ? 0 : 1117;
        }

        private static int Count(Assembly assembly)
        {
            int count = 0;
            var attributes = Query(assembly).GetEnumerator();
            while (attributes.MoveNext()) count++;
            attributes.Dispose();
            return count;
        }

        public static int DynamicAssemblySelection()
        {
            int empty = Count(typeof(EmptyMetadata.Marker).Assembly);
            int full = Count(typeof(Marker).Assembly);
            return empty == 0 && full == 4 ? 1123 : 0;
        }

        public static int FreshInstances()
        {
            var first = Query(typeof(Marker).Assembly).GetEnumerator();
            var second = Query(typeof(Marker).Assembly).GetEnumerator();
            return first.MoveNext() && second.MoveNext() && !ReferenceEquals(first.Current, second.Current) ? 1129 : 0;
        }

        public static int EmptyArrayIdentity()
        {
            var assembly = typeof(EmptyMetadata.Marker).Assembly;
            var first = Query(assembly);
            var second = Query(assembly);
            return ReferenceEquals(first, second) ? 1153 : 1157;
        }

        public static int NullAssembly()
        {
            try { _ = Query(null); }
            catch (ArgumentNullException) { return 1151; }
            return 0;
        }
    }
}
