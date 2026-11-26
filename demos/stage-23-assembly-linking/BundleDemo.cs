namespace BundleLibrary
{
    public static class BundleDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }

    public static class Kernels
    {
        // The CLR builds separate assembly dependencies. Both overloads and the
        // transitive LeafMath reference must resolve to their own definitions.
        public static int Calculate(int value) => unchecked(SharedMath.MathLibrary.Blend(value) +
            SharedMath.MathLibrary.Scale((uint)value));
    }
}
