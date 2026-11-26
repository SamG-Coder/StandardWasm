extern alias Custom;

namespace MathIdentityProbe
{
    // The CLR returns 11 for Compute(9) because it calls the third-party method. The portable compiler must not
    // substitute f64.sqrt (which would give 3), so it must reject this call.
    public static class Probe
    {
        public static double Compute(double value) => Custom::System.Math.Sqrt(value);
    }
}
