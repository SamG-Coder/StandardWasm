using TypedPointerDependency;

namespace TypedPointerDemo
{
    public static unsafe class Kernels
    {
        public static int Run(int marker)
        {
            var slot = new DrawingSlot();
            if (slot.Font != null || slot.Pen != null || slot.Signed != null || slot.Unsigned != null || slot.Fonts != null)
                return -1;

            NativeFont* font = slot.Font;
            slot.Set(marker == 0 ? null : font);
            slot.Pen = null;
            slot.Signed = null;
            slot.Unsigned = null;
            slot.Fonts = null;
            return slot.Score(slot.Font) + slot.Score(slot.Pen) +
                (slot.ReadFonts() == null ? 9 : -1) + (marker == 0 ? 0 : 1);
        }

        public static int Read(DrawingSlot slot) => slot.Font == null ? 1 : -1;

        public static int PickPrimitive() =>
            DrawingSlot.Pick((int*)null) + 10 * DrawingSlot.Pick((uint*)null) + 100 * DrawingSlot.Pick((long*)null);

        public static int ReadPrimitive(DrawingSlot slot) => slot.ReadSigned(null);

        // Since Stage 143 (decision 0071), a pointer to a blittable struct is a native
        // address: any value can be stored, and a null pointer exports as 0.
        public static int StoreFontAddress()
        {
            var slot = new DrawingSlot();
            slot.Font = (NativeFont*)1;
            return slot.Font == (NativeFont*)1 ? 1 : 0;
        }

        // A default int* field is null: ldind.i4 raises NullReferenceException (Stage 132).
        public static int DereferenceNullField() => *new DrawingSlot().Signed;
        public static NativeFont* ExportNullFont() => null;
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
