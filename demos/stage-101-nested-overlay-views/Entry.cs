using NestedOverlayDependency;

namespace NestedOverlayDemo
{
    public static class Kernels
    {
        public static int ReadPair(long bits)
        {
            Overlay value = default;
            value.Raw = bits;
            Pair<int> pair = value.Pair;
            return unchecked(pair.First - pair.Second);
        }

        public static long UpdatePair(long bits, int second)
        {
            Overlay value = default;
            value.Raw = bits;
            Pair<int> pair = value.Pair;
            pair.Second = second;
            value.Pair = pair;
            return value.Raw;
        }

        public static int ReadColor(long bits)
        {
            Overlay value = default;
            value.Raw = bits;
            Color color = value.Color;
            return unchecked(color.Argb ^ color.Known ^ color.State);
        }

        public static long UpdateMiddle(long bits, int word)
        {
            Overlay value = default;
            value.Raw = bits;
            Middle middle = value.Middle;
            middle.Second = unchecked((short)word);
            value.Middle = middle;
            return value.Raw;
        }

        public static long WriteMiddle(long bits, int word)
        {
            Overlay value = default;
            value.Raw = bits;
            Middle middle = default;
            middle.Second = unchecked((short)word);
            value.Middle = middle;
            return value.Raw;
        }

        public static int CopyAndRead(int first, int second)
        {
            Pair<int> pair = default;
            pair.First = first;
            pair.Second = second;
            Overlay value = default;
            value.Pair = pair;
            Overlay copy = value;
            value.Raw = 0;
            Pair<int> observed = copy.Pair;
            return unchecked(observed.First + observed.Second);
        }

        public static long PackDefault(long bits)
        {
            Overlay value = default;
            value.Raw = bits;
            Middle middle = default;
            value.Middle = middle;
            return value.Raw;
        }

        public static int RejectPadded(PaddedOverlay value) => value.Value.First;
        public static int RejectMixedAuto(MixedOverlay value) => value.Value.First;
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
