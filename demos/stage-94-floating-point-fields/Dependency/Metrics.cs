namespace FloatFieldDependency
{
    public class FontMetrics
    {
        public float Size;
    }

    public sealed class ScaledMetrics : FontMetrics
    {
        public double Advance;
    }

    public struct Measure
    {
        public float Size;
        public double Advance;
    }
}
