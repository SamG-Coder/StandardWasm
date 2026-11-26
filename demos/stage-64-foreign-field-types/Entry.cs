using ForeignFieldLeaf;
using ForeignFieldOwner;

namespace ForeignFieldTypes
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var cell = new Cell();
            var node = new Node(value);
            cell.Child = node;
            cell.Alternate = node;
            node.Next = node;
            return unchecked(cell.Child.Next.Value + cell.Read() + (cell.Child == cell.Alternate ? 1 : 0));
        }

        public static int DefaultFields()
        {
            var cell = new Cell();
            return (cell.Child == null ? 1 : 0) + (cell.Alternate == null ? 2 : 0);
        }

        public static int NullRead() => new Cell().Child.Value;
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
