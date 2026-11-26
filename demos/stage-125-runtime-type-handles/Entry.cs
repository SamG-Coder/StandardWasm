extern alias Peer;

using System;
using ExternalTypes = Peer::RuntimeTypeDependency.ExternalTypes;
using LinkedMarker = Peer::RuntimeTypeFixtures.Marker;

namespace RuntimeTypeFixtures
{
    // The dependency declares a distinct type with this same full name.
    public sealed class Marker { }
    public sealed class Other { }
    public class RuntimeBase { }
    public sealed class FirstRuntimeChild : RuntimeBase { }
    public sealed class SecondRuntimeChild : RuntimeBase { }

    public sealed class Uninitialized
    {
        static Uninitialized() => throw new InvalidOperationException();
    }
}

namespace RuntimeTypeDemo
{
    using RuntimeTypeFixtures;

    public static class Kernels
    {
        private static bool Same(object left, object right) => left == right;

        private static Type Resolve(RuntimeTypeHandle handle) => Type.GetTypeFromHandle(handle);

        public static int Local(int selection)
        {
            Type selected = selection > 0 ? typeof(Marker) : typeof(Other);
            return Same(selected, typeof(Marker)) ? 1 : 0;
        }

        public static int RuntimeClass(int selection)
        {
            RuntimeBase instance;
            if (selection < 0) instance = new FirstRuntimeChild();
            else if (selection == 0) instance = null;
            else if (selection == 1) instance = new RuntimeBase();
            else instance = new SecondRuntimeChild();
            try
            {
                return Classify(instance);
            }
            catch (NullReferenceException)
            {
                return -1;
            }
        }

        private static int Classify(RuntimeBase instance)
        {
            Type actual = instance.GetType();
            if (actual == typeof(RuntimeBase)) return 1;
            if (actual == typeof(FirstRuntimeChild)) return 2;
            return actual == typeof(SecondRuntimeChild) ? 3 : 0;
        }

        public static int Linked(int selection)
        {
            Type selected = selection > 0 ? typeof(LinkedMarker) : typeof(Marker);
            return Same(selected, ExternalTypes.Definition()) ? 1 : 0;
        }

        public static int DefaultHandle(int fallback) => Same(Resolve(default(RuntimeTypeHandle)), null) ? 43 : fallback;

        public static int WithoutInitializer(int value) => Same(typeof(Uninitialized), null) ? 0 : value;

        public static int CompareHostObject(object other) => Same(other, typeof(Marker)) ? 1 : 0;

        public static int ConstructedType() => Same(typeof(System.Collections.Generic.List<int>), null) ? 0 : 1;

        public static int RequiresPeer() => Same(typeof(LinkedMarker), null) ? 0 : 1;

        private static object WidenType() => typeof(Marker);

        public static int RequiresTypeTest() => WidenType() is Type ? 1 : 0;

        public static int BoxHandle()
        {
            RuntimeTypeHandle handle = default;
            object boxed = handle;
            return boxed == null ? 0 : 1;
        }
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
