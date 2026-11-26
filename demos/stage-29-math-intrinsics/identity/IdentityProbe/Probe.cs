extern alias Custom;

namespace IdentityProbe
{
    // Regression fixture for the intrinsic identity check (#63 review). The CLR runs the third-party method and
    // returns 11 for Compute(9). The compiler must report an unlinked call, never substitute Math.Sqrt (3).
    public static class Probe
    {
        public static double Compute(double value) => Custom::System.Math.Sqrt(value);
    }
}
