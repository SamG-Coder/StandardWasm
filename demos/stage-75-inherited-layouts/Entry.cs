using HierarchyBase;
using HierarchyMiddle;

namespace HierarchyDemo
{
    public sealed class Leaf : Middle
    {
        public new int Value;

        public Leaf(int value) : base(value)
        {
            Value = unchecked(value + 13);
        }
    }

    public static class Kernels
    {
        private static int ReadBase(Root value) => value.Read();

        public static int Run(int value)
        {
            var leaf = new Leaf(value);
            Root root = leaf;
            root.Value = unchecked(root.Value + 1);
            if (root.Peer != root) return -1;
            return unchecked(ReadBase(leaf) * 3 + leaf.Adjusted * 5 + leaf.Value * 7);
        }

        public static int Cast(int value)
        {
            object instance = new Leaf(value);
            var root = (Root)instance;
            var leaf = (Leaf)root;
            return unchecked(root.Value + leaf.Value);
        }
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
