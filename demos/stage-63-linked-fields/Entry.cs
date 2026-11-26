extern alias left;
extern alias right;
using LeftCell = left::FieldData.Cell;
using RightCell = right::FieldData.Cell;

namespace LinkedFields
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var left = new LeftCell(value);
            var right = new RightCell("abc");
            var next = new LeftCell(unchecked(value + 1));
            left.Next = next;
            left.Count += 9;
            right.Numbers = new int[2];
            right.Numbers[1] = value;
            return unchecked((int)(left.Next.Count + left.Count) + right.Text.Length + right.Numbers[1] + left.Seed);
        }

        public static int ReadNull() => (int)((LeftCell)null).Count;

        public static void WriteNull(int value) => ((LeftCell)null).Count = value;
    }

    public static class LinkDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
