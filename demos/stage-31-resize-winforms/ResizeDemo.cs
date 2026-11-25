using CounterExample;

namespace ResizeLibrary
{
    public static class ResizeDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class LayoutCases
    {
        public static int ReadBounds(int width, int height, int finalWidth, int finalHeight, int events, int row, int column)
        {
            var layout = CounterHandlers.BuildTree();
            var click = CounterHandlers.Bind(CounterHandlers.Create(0));
            CounterHandlers.Resize(layout, width, height);
            for (int i = 0; i < events; i++)
                CounterHandlers.Dispatch(click, layout);
            CounterHandlers.Resize(layout, finalWidth, finalHeight);
            return row < CounterHandlers.ControlCount(layout) ? CounterHandlers.ControlValue(layout, row, column) : -1;
        }
    }
}
