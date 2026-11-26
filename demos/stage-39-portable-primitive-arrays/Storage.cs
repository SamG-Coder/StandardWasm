namespace PrimitiveStorage
{
    public static class StorageDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class StorageCases
    {
        public static int ReadSigned(int value)
        {
            var data = new short[1];
            data[0] = unchecked((short)value);
            return data[0];
        }

        public static int ReadUnsigned(int value)
        {
            var data = new ushort[1];
            data[0] = unchecked((ushort)value);
            return data[0];
        }

        public static int ReadSignedByte(int value)
        {
            var data = new sbyte[1];
            data[0] = unchecked((sbyte)value);
            return data[0];
        }

        public static int CountFlags(int size)
        {
            var flags = new bool[size];
            for (int i = 0; i < flags.Length; i++)
                flags[i] = (i & 1) != 0;
            int count = 0;
            for (int i = 0; i < flags.Length; i++)
                if (flags[i]) count++;
            return count;
        }

        public static int ReadUnsignedBits(int value)
        {
            var data = new uint[1];
            data[0] = unchecked((uint)value);
            return data[0] > int.MaxValue ? 1 : 0;
        }

        public static int PackedLength(int length) => new short[length].Length;

        public static float ReadSingle(float value, int index)
        {
            var storage = new FloatingStorage();
            storage.Single = new float[2];
            storage.Single[0] = value;
            return SingleAt(storage.Single, index);
        }

        public static double ReadDouble(double value, int index)
        {
            var storage = new FloatingStorage();
            storage.Double = new double[2];
            storage.Double[0] = value;
            return DoubleAt(storage.Double, index);
        }

        private static float SingleAt(float[] data, int index) => data[index];
        private static double DoubleAt(double[] data, int index) => data[index];
    }

    internal sealed class FloatingStorage
    {
        internal float[] Single;
        internal double[] Double;
    }
}
