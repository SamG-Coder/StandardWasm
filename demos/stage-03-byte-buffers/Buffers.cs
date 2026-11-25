namespace ByteLibrary
{
    public static class Buffers
    {
        public static int RoundTrip(int count, int index, int value)
        {
            var buffer = new byte[count];
            buffer[index] = unchecked((byte)value);
            return Read(buffer, index) + buffer.Length * 256;
        }

        public static int ZeroInitialized(int count, int index)
        {
            return Read(new byte[count], index);
        }

        public static int Narrow(int value) => unchecked((byte)value);

        private static byte Read(byte[] buffer, int index) => buffer[index];
    }
}
