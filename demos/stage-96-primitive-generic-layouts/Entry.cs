using PrimitiveGenericDependency;

namespace PrimitiveGenericDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame();
            if (frame.Signed.Key != 0 || frame.Unsigned.Key != 0 || frame.Measures != null) return -1;

            Pair<int, Label> signed = default;
            signed.Key = value;
            var keys = new int[1];
            keys[0] = value + 2;
            signed.Keys = keys;
            var label = new Label(5);
            signed.Value = label;
            frame.Signed = signed;
            signed.Key = value + 1;

            Pair<uint, Label> unsigned = default;
            unsigned.Key = unchecked((uint)value);
            frame.Unsigned = unsigned;
            return unchecked(frame.Signed.Key + frame.Signed.Keys[0] + frame.Signed.Value.Marker +
                (int)frame.Unsigned.Key + signed.Key);
        }

        public static int Narrow(int value)
        {
            Pair<sbyte, Label> item = default;
            item.Key = unchecked((sbyte)value);
            return item.Key;
        }

        public static double Measure(double value)
        {
            Pair<double, Label> item = default;
            if (item.Key != 0) return -1;
            item.Key = value;
            return item.Key;
        }

        public static int Read(Frame frame)
        {
            Pair<int, Label> item = frame.Signed;
            return item.Key;
        }

        public static int ReadVector(Frame frame) => frame.SignedVectors.Key == null ? 1 : 0;
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
