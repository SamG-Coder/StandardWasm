extern alias Custom;

namespace ArrayIdentityProbe
{
    // Regression fixture from Codex's review of #75. On the CLR, Make(1) is an array of a third-party class and
    // cannot be passed as short[]. The compiler must reject the third-party element type, never lower it as int16.
    public static class Probe
    {
        public static Custom::System.Int16[] Make(int count) => new Custom::System.Int16[count];

        public static int Read(short[] values) => values[0];
    }
}
