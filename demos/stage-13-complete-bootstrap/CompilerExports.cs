namespace BootstrapLibrary
{
    public static class CompilerExports
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
        public static int CompileModule(byte[] image, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileExports(image, entries, output);
        public static int BootstrapKernel(int value) => value * 2 + 1;
    }
}
