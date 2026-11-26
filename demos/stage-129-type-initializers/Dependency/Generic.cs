namespace TypeInitializerDependency
{
    // beforefieldinit generic: a static call that touches no static field does not
    // trigger it (Stage 108), so the call compiles without a per-instantiation guard.
    public static class RelaxedBox<T>
    {
        private static int initialized = 13;

        public static int Id(int value) => value + 5;

        public static int Read() => initialized;
    }

    // Precise generic: each closed instantiation needs its own guard, which the
    // compiler does not provide yet. Calls stay a located diagnostic.
    public static class PreciseBox<T>
    {
        private static int initialized;

        static PreciseBox() => initialized = 17;

        public static int Id(int value) => value + 7 + initialized;
    }
}
