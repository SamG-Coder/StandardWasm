namespace FloatingPoint
{
    // Negative fixtures. Floating-point remainder has no Wasm instruction and needs an exact fmod, and checked
    // float-to-int conversions throw OverflowException on the CLR. Both must be diagnosed, never approximated.
    public static class CheckedReals
    {
        public static double Remainder(double a, double b) => a % b;
    }

    public static class CheckedConversions
    {
        public static int Narrow(double value) => checked((int)value);
    }
}
