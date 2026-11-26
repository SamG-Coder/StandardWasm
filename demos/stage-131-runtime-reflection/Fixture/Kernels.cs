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

        public static int ResourceBufferIdentity()
        {
            var bytes = new byte[3];
            var characters = new char[2];
            var integers = new long[2];
            object alias = BufferIdentity.Preserve(bytes);
            object other = BufferIdentity.Preserve(new byte[3]);
            int result = alias == bytes ? 1 : 0;
            if (alias == other) result |= 2;
            if (BufferIdentity.Preserve(characters) == characters) result |= 4;
            if (BufferIdentity.Preserve(integers) == integers) result |= 8;
            if (BufferIdentity.Preserve(null) == null) result |= 16;
            return result;
        }

        public static int OrdinalResourceNames()
        {
            string name = "Resource.é.東京.𐐀";
            int result = OperatorEquals(name, "Resource.é.東京.𐐀") ? 1 : 0;
            if (OperatorEquals(name, "Resource.é.東京.𐐁")) result |= 2;
            if (StaticEquals(name, "resource.é.東京.𐐀")) result |= 4;
            if (StaticEquals(name, "Resource.e\u0301.東京.𐐀")) result |= 8;
            if (StaticEquals(name, "Resource.é.東京.𐐀\0")) result |= 16;
            return result;
        }

        public static int OrdinalNullAndEmpty()
        {
            int result = OperatorEquals(null, null) ? 1 : 0;
            if (OperatorEquals(null, "")) result |= 2;
            if (StaticEquals("", "")) result |= 4;
            if (StaticEquals("", null)) result |= 8;
            return result;
        }

        public static int OrdinalSeparateStrings()
        {
            var units = new char[2];
            units[0] = 'é';
            units[1] = '京';
            var first = new string(units);
            var second = new string(units);
            return OperatorEquals(first, second) ? 1 : 0;
        }

        private static bool OperatorEquals(string left, string right) => left == right;
        private static bool StaticEquals(string left, string right) => string.Equals(left, right);

        public static int NullModule()
        {
            Module module = null;
            try { return module.Name.Length; }
            catch (NullReferenceException) { return 16; }
        }
    }
}
