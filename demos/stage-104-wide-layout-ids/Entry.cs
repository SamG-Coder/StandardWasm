using WideLayoutDependency;

namespace WideLayoutDemo
{
    public static class Kernels
    {
        public static int Run(int value, int mode)
        {
            var table = new LayoutTable();
            table.First = new Layout000();
            table.First.Value = value;
            table.Last = new LastLayout();
            table.Last.Value = unchecked(value + 3);
            object candidate = table.Last;
            if (mode != 0) candidate = table.First;

            try
            {
                LastLayout selected = (LastLayout)candidate;
                return unchecked(selected.Score() + table.First.Value);
            }
            catch (System.InvalidCastException)
            {
                return table.First.Value;
            }
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
