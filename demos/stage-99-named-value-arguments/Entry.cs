using NamedValueDependency;

namespace NamedValueDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame();
            Slot<Left> emptyLeft = frame.Left;
            Slot<Right> emptyRight = frame.Right;
            Left leftDefault = emptyLeft.Value;
            Right rightDefault = emptyRight.Value;
            if (leftDefault.Marker != 0 || rightDefault.Marker != 0) return -1;

            Left left = default;
            left.Marker = value;
            Slot<Left> leftSlot = default;
            leftSlot.Value = left;
            frame.Left = leftSlot;

            Right right = default;
            right.Marker = unchecked(value + 3);
            Slot<Right> rightSlot = default;
            rightSlot.Value = right;
            frame.Right = rightSlot;

            left.Marker = unchecked(value + 1);
            leftSlot.Value = left;
            Slot<Left> storedLeft = frame.Left;
            Slot<Right> storedRight = frame.Right;
            Left leftCopy = storedLeft.Value;
            Right rightCopy = storedRight.Value;
            Left updated = leftSlot.Value;
            return unchecked(leftCopy.Marker + rightCopy.Marker + updated.Marker);
        }

        public static int Read(Frame frame)
        {
            Slot<Left> slot = frame.Left;
            Left value = slot.Value;
            return value.Marker;
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
