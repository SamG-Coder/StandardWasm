using EmbeddedValueDependency;

namespace EmbeddedValueDemo
{
    public static unsafe class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame(5);
            if (frame.State.First != 0 || frame.State.Second != 0 || frame.Context.Value != null)
                return -1;
            Values initial = default;
            initial.First = value;
            initial.Second = unchecked((short)(value + 2));
            frame.State = initial;

            Values copy = ValueCalls.Identity(frame.State);
            copy.First = unchecked(value + 1);
            return unchecked(frame.Marker + frame.State.First * 2 + frame.State.Second * 3 + copy.First * 5 + copy.Second * 7);
        }

        public static int Reset(int value)
        {
            Values local = default;
            local.First = value;
            Values copy = local;
            local = default;
            return unchecked(copy.First * 3 + local.First);
        }

        public static int ReadReceiver(Frame frame) => frame.State.First;

        public static int Assign(int value)
        {
            var frame = new Frame(0);
            frame.State = MakeValues(value);
            return frame.State.First;
        }

        private static Values MakeValues(int value)
        {
            Values values = default;
            values.First = value;
            return values;
        }

        public static OtherValues MakeOther(int value)
        {
            OtherValues values = default;
            values.First = value;
            return values;
        }

        public static int BorrowValue(int value)
        {
            Values local = default;
            local.First = 3;
            Values copy = local;
            Update(ref local, value);
            return unchecked(local.First * 7 + copy.First);
        }

        private static void Update(ref Values local, int value) => local.First = unchecked(local.First + value);
        public static Values RejectExport() => default;
        public static ref Values RejectExportedAddress(ref Values value) => ref value;
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
