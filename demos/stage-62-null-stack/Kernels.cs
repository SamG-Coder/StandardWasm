using NullStackDependency;

namespace NullStack
{
    public sealed class Meter
    {
        public int Value;

        public Meter(int value) => Value = value;
        public int Read() => Value;
    }

    public static class Kernels
    {
        public static int Read(int value) => (value == 0 ? null : new Meter(value)).Read();
        public static int ReadReversed(int value) => (value != 0 ? new Meter(value) : null).Read();
        public static int ReadLinked(int value) => (value == 0 ? null : new RemoteMeter(value)).Read();

        public static int ReadLength(int value) => (value < 0 ? null : new int[value]).Length;
        public static int ReadText(int value) => string.Concat(value == 0 ? null : "abc", "d").Length;
        public static int ReadNullField() => ((Meter)null).Value;
        public static int ReadNullElement() => ((int[])null)[0];

        public static int Loop(int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
                total += ReadOptional((i & 1) == 0 ? new Meter(i + 10) : null);
            return total;
        }

        private static int ReadOptional(Meter meter) => meter == null ? -1 : meter.Read();
    }

    public static class NullDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
