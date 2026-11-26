using GenericStorageDependency;

namespace GenericStorageDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame(value);
            var left = new Left(value);
            frame.Array = new ILeft[2];
            frame.Array[0] = left;
            if (frame.Array[0] != left || frame.Array[1] != null) return -1;
            frame.Array[0] = null;
            if (frame.Array[0] != null || frame.Left != null || frame.Right != null) return -2;
            return unchecked(frame.Marker + frame.Array.Length);
        }

        public static int ReadArray(int index, bool clear)
        {
            var frame = new Frame(0) { Array = new ILeft[1] };
            if (clear) frame.Array = null;
            return frame.Array[index] == null ? 1 : 0;
        }

        public static int Store(int value)
        {
            var frame = new Frame(value);
            frame.Left = ReadLeft(frame);
            return frame.Left == null ? frame.Marker : 0;
        }

        private static Vector<ILeft> ReadLeft(Frame target) => target.Left;
        public static Vector<IRight> ReadRight(Frame target) => target.Right;

        public static int ReadConstructed(int value) => new Vector<ILeft> { Count = value }.Count;
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
