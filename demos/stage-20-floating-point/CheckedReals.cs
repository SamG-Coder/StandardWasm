namespace FloatingPoint
{
    // Negative fixture. A checked float-to-int conversion throws OverflowException on the CLR and must be diagnosed,
    // never approximated. (Float remainder, formerly rejected here, is lowered exactly since stage 33.)
    public static class CheckedConversions
    {
        public static int Narrow(double value) => checked((int)value);
    }
}
