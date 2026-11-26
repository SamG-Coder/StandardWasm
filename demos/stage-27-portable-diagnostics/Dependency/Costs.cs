namespace DiagnosticDependency
{
    public static class Costs
    {
        public static int Estimate(int hours) => checked(hours * 31);
        public static int FlatRate(int hours) => unchecked(hours * 31);
    }
}
