using System;

namespace RuntimeTypeFixtures
{
    public sealed class Marker { }
}

namespace RuntimeTypeDependency
{
    public static class ExternalTypes
    {
        public static Type Definition() => typeof(RuntimeTypeFixtures.Marker);
    }
}
