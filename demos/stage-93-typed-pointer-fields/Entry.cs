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

        public static int RejectAddress(DrawingSlot slot)
        {
            slot.Font = (NativeFont*)1;
            return 0;
        }

        public static int RejectDereference(DrawingSlot slot) => *slot.Signed;
        public static NativeFont* RejectExport() => null;
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
