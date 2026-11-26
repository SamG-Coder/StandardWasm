using EnumArgumentDependency;

namespace EnumArgumentDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame();
            Slot<Left> empty = frame.Left;
            if (empty.Value != 0) return -1;

            Slot<Left> left = default;
            left.Value = unchecked((Left)value);
            frame.Left = left;
            Slot<Right> right = default;
            right.Value = unchecked((Right)(value + 1));
            frame.Right = right;

            left.Value = 0;
            Slot<Left> savedLeft = frame.Left;
            Slot<Right> savedRight = frame.Right;
            return unchecked((int)savedLeft.Value + (int)savedRight.Value + (int)left.Value);
        }

        public static int Read(Frame frame)
        {
            Slot<Left> value = frame.Left;
            return (int)value.Value;
        }
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
