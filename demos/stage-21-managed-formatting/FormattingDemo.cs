using StandardWasm.Managed;

namespace FormattingLibrary
{
    public static class FormattingDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
        public static int CompileModule(byte[] image, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileExports(image, entries, output);
    }

    public static class Kernels
    {
        public static string Format(int value) => InvariantText.Int32(value);
        public static int Unit(int value, int index) => Format(value)[index];
        public static int TextLength(string value) => value.Length;
        public static int TextCodeUnit(string value, int index) => value[index];

        public static string Join(int mode)
        {
            if (mode == 0) return string.Concat(null, "a\0\u03a9");
            if (mode == 1) return string.Concat("a\0\u03a9", null);
            if (mode == 2) return string.Concat((string)null, (string)null);
            if (mode == 3) return string.Concat("", "\ud83d\ude80");
            return string.Concat("\ud83d", "\ude80\0\u03a9");
        }

        public static string CopyThenMutate(int value)
        {
            var buffer = new char[2];
            buffer[0] = (char)value;
            buffer[1] = buffer[0];
            string first = new string(buffer);
            buffer[0] = '!';
            return first;
        }

        public static string FromNullableArray(char[] value) => new string(value);
    }
}
