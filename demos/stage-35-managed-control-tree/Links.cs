namespace ReferenceLibrary
{
    public static class ReferenceDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    internal sealed class Root
    {
        internal Node Child;
    }

    internal sealed class Node
    {
        internal Root Owner;
        internal Node Next;
        internal int Value;
    }

    public static class Links
    {
        public static int Chain(int value)
        {
            var tree = new Root();
            if (tree.Child != null) return -1;
            var node = new Node();
            tree.Child = node;
            node.Owner = tree;
            node.Next = node;
            Set(tree.Child, value);
            return node.Next.Owner.Child.Value;
        }

        public static bool Aliases(bool same)
        {
            var node = new Node();
            var root = new Root();
            root.Child = same ? node : new Node();
            return root.Child == node;
        }

        public static int NullRead()
        {
            var root = new Root();
            return Get(root.Child);
        }

        private static void Set(Node node, int value) => node.Value = value;
        private static int Get(Node node) => node.Value;
    }
}
