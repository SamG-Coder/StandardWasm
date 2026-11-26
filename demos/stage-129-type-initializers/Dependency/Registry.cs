namespace TypeInitializerDependency
{
    // A precise (explicit static constructor) type in a bundled dependency. Both a
    // cross-assembly field read and a cross-assembly static call must trigger it.
    public static class Registry
    {
        public static int Value;
        public static int Runs;

        static Registry()
        {
            Runs++;
            Value = 40;
        }

        public static int Read() => Value + 2;
    }
}
