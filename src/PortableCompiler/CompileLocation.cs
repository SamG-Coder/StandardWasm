namespace PortableCompiler
{
    internal static class CompileLocation
    {
        internal const int Size = 3;

        // The empty array is the status-only path. The detailed path owns three
        // slots: bundle index, MethodDef row (one-based), and IL byte offset.
        internal static void Set(int[] location, int module, int method, int offset)
        {
            if (location.Length == 0) return;
            location[0] = module;
            location[1] = method;
            location[2] = offset;
        }

        internal static void Clear(int[] location) => Set(location, -1, 0, -1);
    }
}
