using System;
using System.Reflection;
using System.Resources;

[assembly: NeutralResourcesLanguage("fr-CA")]
[assembly: AssemblyMetadata("ResourceCulture", "fr-CA")]

namespace NeutralResourceLanguage
{
    public static class Probe
    {
        private static NeutralResourcesLanguageAttribute Query(Assembly assembly) =>
            assembly.GetCustomAttribute<NeutralResourcesLanguageAttribute>();

        public static int DeclaredLanguage()
        {
            var attribute = Query(typeof(Probe).Assembly);
            return attribute.CultureName.Length == 5 && attribute.CultureName[0] == 'f' &&
                attribute.CultureName[3] == 'C' && attribute.Location == UltimateResourceFallbackLocation.MainAssembly ? 1 : 0;
        }

        public static int PerAssembly()
        {
            if (Query(typeof(EmptyMetadata.Marker).Assembly) != null) return 0;
            return Query(typeof(Probe).Assembly) != null ? 1 : 0;
        }

        public static int FreshAttributes()
        {
            var assembly = typeof(Probe).Assembly;
            var first = Query(assembly);
            var second = Query(assembly);
            return first != null && second != null && !ReferenceEquals(first, second) ? 1 : 0;
        }

        public static int NullAssembly()
        {
            try { _ = Query(null); }
            catch (ArgumentNullException) { return 1; }
            return 0;
        }

        public static int IndependentQueries()
        {
            var assembly = typeof(Probe).Assembly;
            foreach (var metadata in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if (metadata.Key[0] == 'R' && metadata.Value[0] == Query(assembly).CultureName[0]) return 1;
            return 0;
        }
    }
}
