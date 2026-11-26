namespace FloatRemainder
{
    // Floating-point % in ordinary code. The CLR evaluates it as C fmod: exact, truncated toward zero, with the sign
    // of the dividend. The compiler lowers it to an exact helper function emitted into the module.
    public static class Remainders
    {
        public static double Remainder(double x, double y) => x % y;

        public static float RemainderSingle(float x, float y) => x % y;

        // Wrap an angle in degrees into [0, 360).
        public static double WrapDegrees(double angle)
        {
            double wrapped = angle % 360.0;
            return wrapped < 0 ? wrapped + 360.0 : wrapped;
        }

        // Phase of a periodic signal: fraction of the period elapsed at time t.
        public static double Phase(double t, double period) => (t % period) / period;

        // Fractional part with the sign of the input, e.g. -2.75 -> -0.75.
        public static double Fraction(double value) => value % 1.0;

        // Remainder in a loop, as in a step-wise oscillator, in single precision.
        public static float Oscillate(float step, int count)
        {
            float phase = 0;
            for (int i = 0; i < count; i++)
                phase = (phase + step) % 1.0f;
            return phase;
        }
    }
}
