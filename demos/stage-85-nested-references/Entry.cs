using NestedContract;

namespace NestedDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var left = new Left.Branch.Token(value);
            var right = new Right.Branch.Token(unchecked(value + 1));
            var holder = new Holder { Value = left };
            if (holder.Value != left || IsLeft(right)) return -1;
            return unchecked(left.Value + right.Value);
        }

        private static bool IsLeft(object value) => value is Left.Branch.Token;

        public static int Friend(int value) => new Hidden.Token(value).Value;

        public static int Store(int value)
        {
            var holder = new Holder();
            holder.Value = MakeLeft(value);
            return holder.Value == null ? 0 : 1;
        }

        private static Left.Branch.Token MakeLeft(int value) => new Left.Branch.Token(value);
        public static Right.Branch.Token MakeRight(int value) => new Right.Branch.Token(value);
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
