namespace FloatDependency
{
    public static class Scale
    {
        public static double Adjust(float prior, double delta) => prior * 1.25 + delta;
        public static float Adjust(float prior, float delta) => prior * 1.25f + delta;
    }
}
