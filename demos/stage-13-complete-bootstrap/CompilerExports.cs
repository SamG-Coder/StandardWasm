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
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
        public static int[] CreateDiagnostic() => new int[PortableCompiler.CompileLocation.Size];
        public static int ReadDiagnostic(int[] location, int index) => location[index];
        public static int CompileDetailed(byte[] image, int row, byte[] output, int[] location) =>
            PortableCompiler.MethodCompiler.CompileDetailed(image, row, output, location);
        public static int CompileModuleDetailed(byte[] image, byte[] entries, byte[] output, int[] location) =>
            PortableCompiler.MethodCompiler.CompileModuleDetailed(image, entries, output, location);
        public static int CompileBundleDetailed(byte[] bundle, byte[] entries, byte[] output, int[] location) =>
            PortableCompiler.MethodCompiler.CompileBundleDetailed(bundle, entries, output, location);
        public static int BundleModuleOrigins() => 1;
        public static int ReadWebcilEntryToken(byte[] image)
        {
            int token = PeLibrary.WebcilLocator.ReadEntryToken(image);
            if (token < 0 || (token & 0xffffff) > PeLibrary.CompressedTables.Find(image, new int[4]))
                return -1;
            return token;
        }
        public static int BootstrapKernel(int value) => value * 2 + 1;
    }
}
