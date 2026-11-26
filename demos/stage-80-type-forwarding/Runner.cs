using Forwarded;

namespace ForwardingDemo
{
    public static class Entry
    {
        public static int Run(int input)
        {
            var box = new Box(input);
            box.Value = unchecked(box.Value + 17);
            object alias = Operations.Identity(box);
            return ((Box)alias).Read();
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
