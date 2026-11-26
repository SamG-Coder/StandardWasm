namespace TypedPointerDependency
{
    // The pointer layout does not need the representation of either pointee.
    public struct NativeFont { public double OpaqueData; }
    public struct NativePen { public double OpaqueData; }

    public sealed unsafe class DrawingSlot
    {
        public NativeFont* Font;
        public NativePen* Pen;
        public int* Signed;
        public uint* Unsigned;
        public NativeFont** Fonts;

        public void Set(NativeFont* value) => Font = value;

        public int Score(NativeFont* value) => value == null ? 11 : -1;
        public int Score(NativePen* value) => value == null ? 17 : -1;

        public NativeFont** ReadFonts() => Fonts;
    }
}
