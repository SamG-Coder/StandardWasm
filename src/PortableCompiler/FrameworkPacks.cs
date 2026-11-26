using PeLibrary;

namespace PortableCompiler
{
    // CLR-to-Wasm: this target lowers native int and native unsigned int to
    // i32 (decision 0027). A CoreLib built for a 64-bit target encodes that
    // width in its IL, for example `ldc.i4 8` for IntPtr.Size and an `ldc.i8`
    // for IntPtr.MaxValue. Lowering such a body into i32 silently changes its
    // result, so every CoreLib body must come from a CoreLib built for 32 bits.
    //
    // Other assemblies, including AnyCPU application and library DLLs, are
    // compiled from architecture-neutral IL and aren't checked here.
    internal static class FrameworkPacks
    {
        // The bundle supplies a CoreLib whose bodies assume another native
        // width. The location names that bundle entry and the MethodDef row
        // that would have been lowered first.
        internal const int IncompatibleNativeWidth = -7;

        internal const int TargetNativeWidth = 4;

        // The cache holds the reader's result plus Bias, so zero means unread.
        private const int Bias = 3;

        internal static int CheckBodyOwner(byte[] image, int[] layout, int[] heaps, int[] widths, int owner)
        {
            if (widths[owner] == 0) widths[owner] = CoreLibNativeWidth.Read(image, layout, heaps) + Bias;
            int width = widths[owner] - Bias;
            if (width == CoreLibNativeWidth.NotCoreLib || width == TargetNativeWidth) return 0;
            return width == CoreLibNativeWidth.Malformed ? -1 : IncompatibleNativeWidth;
        }
    }
}
