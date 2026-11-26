using System;
using System.Reflection;
using ReflectionDependency;

namespace ReflectionFixture
{
    public sealed class Neighbor { }

    public static class Kernels
    {
        public static int ModuleName()
        {
            string name = typeof(Kernels).Module.Name;
            return name.Length * 65536 + name[0];
        }

        public static int DependencyModuleName()
        {
            string name = typeof(Imported).Module.Name;
            return name.Length * 65536 + name[0];
        }

        public static int ModuleIdentity()
        {
            object own = typeof(Kernels).Module;
            object neighbor = typeof(Neighbor).Module;
            object other = typeof(Imported).Module;
            return (own == neighbor ? 1 : 0) + (own == other ? 2 : 0);
        }

        public static int AssemblyIdentity()
        {
            object own = typeof(Kernels).Assembly;
            object moduleOwner = typeof(Neighbor).Module.Assembly;
            object other = typeof(Imported).Module.Assembly;
            return (own == moduleOwner ? 4 : 0) + (own == other ? 8 : 0);
        }

        public static int NullModule()
        {
            Module module = null;
            try { return module.Name.Length; }
            catch (NullReferenceException) { return 16; }
        }
    }
}
