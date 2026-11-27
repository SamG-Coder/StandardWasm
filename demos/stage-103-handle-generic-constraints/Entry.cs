using HandleConstraintDependency;

namespace HandleConstraintDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame();
            HandleSlot<Handle> empty = frame.Slot;
            Handle initial = empty.Value;
            if (initial.Marker != 0) return -1;

            Handle handle = default;
            handle.Marker = value;
            HandleSlot<Handle> slot = default;
            slot.Value = handle;
            frame.Slot = slot;
            handle.Marker = 0;
            slot.Value = handle;
            HandleSlot<Handle> saved = frame.Slot;
            Handle actual = saved.Value;
            return ShapeMethods.Request<DerivedShapeProvider>(actual.Marker);
        }

        public static int Read(Receiver<Handle> handle) => 42;
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
