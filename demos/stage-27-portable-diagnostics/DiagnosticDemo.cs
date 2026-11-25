namespace DiagnosticLibrary
{
    public static class DiagnosticDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int[] CreateDiagnostic() => new int[PortableCompiler.CompileLocation.Size];
        public static int ReadDiagnostic(int[] location, int index) => location[index];
        public static int CompileDetailed(byte[] image, int row, byte[] output, int[] location) =>
            PortableCompiler.MethodCompiler.CompileDetailed(image, row, output, location);
    }

    public static class Estimates
    {
        public static int CheckedIncrement(int value) => checked(value + 1);
        public static int ViaHelper(int value) => ClampCost(value) + 1;
        private static int ClampCost(int value) => checked(value * 3);

        public static int ViaDependency(int value) => DiagnosticDependency.Costs.Estimate(value) + 1;
        public static int LinkedRate(int value) => DiagnosticDependency.Costs.FlatRate(value) + 1;

        public static int Absolute(int value) => System.Math.Abs(value);
        public static double UnsupportedShape(int value) => value;
        public static int Supported(int value) => unchecked(value * 3 + 1);
    }
}
