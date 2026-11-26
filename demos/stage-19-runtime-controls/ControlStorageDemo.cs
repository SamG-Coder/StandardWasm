namespace ControlStorageLibrary
{
    public static class ControlStorageDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    internal sealed class State
    {
        private const int ValueMask = 1023;
        private readonly int[] bounds;
        private readonly byte[] style;

        internal State()
        {
            bounds = new int[1];
            style = new byte[1];
        }

        internal void Set(int input)
        {
            bounds[0] = input;
            style[0] = unchecked((byte)input);
        }

        internal int Value() => (bounds[0] & ValueMask) + style[0];
    }

    public static class Kernels
    {
        public static int Change(int input)
        {
            var state = new State();
            state.Set(input);
            return state.Value();
        }
    }
}
