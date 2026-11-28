namespace DiagnosticDependency
{
    public static class Costs
    {
        public static int Estimate(int hours) => checked(hours * 31);
        public static int ConvertHours(int hours) => checked((byte)hours);
        public static int BoxHours(int hours) => ReadObject((HourValue)hours);
        private static int ReadObject(object hours) => (int)hours;
        public static int FlatRate(int hours) => unchecked(hours * 31);
        public static int UnsupportedTypedReference(int hours) => __refvalue(__makeref(hours), int);
        private enum HourValue { }
    }
}
