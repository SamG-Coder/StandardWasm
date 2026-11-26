using FloatFieldDependency;

namespace FloatFieldDemo
{
    public static class Kernels
    {
        public static double Run(double size, double advance)
        {
            var metrics = new ScaledMetrics();
            metrics.Size = (float)size;
            metrics.Advance = advance;
            return metrics.Advance - metrics.Size;
        }

        public static float SingleRoundTrip(float value)
        {
            var metrics = new ScaledMetrics();
            metrics.Size = value;
            return metrics.Size;
        }

        public static double DoubleRoundTrip(double value)
        {
            var metrics = new ScaledMetrics();
            metrics.Advance = value;
            return metrics.Advance;
        }

        public static float DefaultSingle() => new ScaledMetrics().Size;
        public static double DefaultDouble() => new ScaledMetrics().Advance;
        public static float Read(FontMetrics metrics) => metrics.Size;
        public static void Write(ScaledMetrics metrics, double value) => metrics.Advance = value;

        public static double CopyMeasure(double advance, float size)
        {
            Measure measure = default;
            measure.Advance = advance;
            Measure copy = measure;
            copy.Size = size;
            return copy.Advance - copy.Size + measure.Size;
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
