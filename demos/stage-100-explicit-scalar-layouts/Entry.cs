using ScalarOverlayDependency;

namespace ScalarOverlayDemo
{
    public static class Kernels
    {
        public static int MiddleWrite(int bits, int middle)
        {
            Overlay value = default;
            value.Full = bits;
            value.Middle = unchecked((short)middle);
            return value.Full;
        }

        public static int ReadMiddle(int bits)
        {
            Overlay value = default;
            value.Full = bits;
            return value.Middle;
        }

        public static int CopyAndWrite(int bits, int high)
        {
            Overlay first = default;
            first.Full = bits;
            var frame = new Frame();
            frame.Value = first;
            first.High = unchecked((sbyte)high);
            Overlay copy = frame.Value;
            return copy.Full ^ first.Full;
        }

        public static int SignedHigh(int bits)
        {
            Overlay value = default;
            value.Full = bits;
            return value.High;
        }

        public static int DefaultParts()
        {
            var frame = new Frame();
            Overlay value = frame.Value;
            return value.Full + value.Middle + value.High;
        }

        public static int SingleBits(int bits)
        {
            FloatOverlay first = default;
            first.Bits = bits;
            float value = first.Value;
            FloatOverlay second = default;
            second.Value = value;
            return second.Bits;
        }

        public static long DoubleBits(long bits)
        {
            DoubleOverlay first = default;
            first.Bits = bits;
            double value = first.Value;
            DoubleOverlay second = default;
            second.Value = value;
            return second.Bits;
        }

        public static long CrossWord(long bits, int value)
        {
            DoubleOverlay overlay = default;
            overlay.Bits = bits;
            overlay.CrossWord = value;
            return overlay.Bits;
        }

        public static int UnsignedHigh(long bits)
        {
            DoubleOverlay overlay = default;
            overlay.Bits = bits;
            return overlay.High;
        }

        public static int RejectLarge(LargeFrame frame) => frame == null ? 0 : 1;
        public static int RejectNested(NestedFrame frame) => frame == null ? 0 : 1;
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
