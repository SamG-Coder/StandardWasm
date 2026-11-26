namespace InitializerDependency
{
    public static class Independent
    {
        public static int Read(int value) => unchecked(value * 3);
    }

    public static class NeedsInitializer
    {
        static NeedsInitializer() => int.Parse("initializer probe");
        public static int Read(int value) => value;
    }
}
