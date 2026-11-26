using InitializerDependency;

namespace InitializerReachability
{
    public static class LocalInitializer
    {
        static LocalInitializer() => int.Parse("local initializer probe");
        public static int Read(int value) => value;
    }

    public static class Kernels
    {
        public static int Safe(int value) => unchecked(value + 11);
        public static int SafeBundle(int value) => unchecked(Independent.Read(value) + 7);
        public static int ReachedLocal(int value) => LocalInitializer.Read(value);
        public static int ReachedDependency(int value) => NeedsInitializer.Read(value);
    }

    public static class InitializerDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
