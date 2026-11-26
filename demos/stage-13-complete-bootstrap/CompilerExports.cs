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
        // The low 32 bits identify the original file offset. The high bits
        // hold the byte length. A negative result is a reader diagnostic.
        public static long FindEmbeddedResource(byte[] image, byte[] utf8Name)
        {
            if (image == null || utf8Name == null) return -1;
            var methods = new int[4];
            var heaps = new int[8];
            int status = PeLibrary.CompressedTables.FindWithHeaps(image, methods, heaps);
            if (status < 0) return status;
            var range = new int[2];
            status = PeLibrary.ManifestResources.Find(image, heaps, methods, utf8Name, range);
            return status < 0 ? status : ((long)range[1] << 32) | (uint)range[0];
        }
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
