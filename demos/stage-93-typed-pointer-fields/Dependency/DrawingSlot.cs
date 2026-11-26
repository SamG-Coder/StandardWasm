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

        public static int Pick(int* value) => value == null ? 1 : -1;
        public static int Pick(uint* value) => value == null ? 2 : -1;
        public static int Pick(long* value) => value == null ? 3 : -1;

        public int ReadSigned(int* value) => value == null ? 7 : -1;
    }
}
