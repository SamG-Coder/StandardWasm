using CounterExample;

namespace DynamicControlsLibrary
{
    public static class DynamicControlsDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class Kernels
    {
        public static int Toggle(int events)
        {
            var layout = CounterHandlers.BuildTree();
            var click = CounterHandlers.Bind(CounterHandlers.Create(0));
            for (int i = 0; i < events; i++)
                CounterHandlers.Dispatch(click, layout);
            return layout.Count() * 100 + layout.Value(0, ControlTree.X);
        }

        public static int Compact(int width)
        {
            var tree = new ControlTree(420, 240, 1, 3);
            tree.Add(1, ControlTree.Label, 0, 0, 10, 10);
            if (tree.Add(1, ControlTree.Button, 0, 0, 10, 10)) return -1;
            tree.Add(2, ControlTree.Label, 10, 10, 10, 10);
            tree.Add(3, ControlTree.Label, 20, 20, width, 10);
            if (tree.Add(4, ControlTree.Button, 30, 30, 10, 10)) return -2;
            if (!tree.Remove(2) || tree.Remove(2)) return -3;
            if (!tree.Add(4, ControlTree.Button, 30, 30, 10, 10)) return -4;
            if (tree.Value(0, ControlTree.Key) != 1 || tree.Value(1, ControlTree.Key) != 3 ||
                tree.Value(2, ControlTree.Key) != 4) return -5;
            return unchecked(tree.Count() * 10000 + tree.Value(1, ControlTree.Width));
        }
    }
}
