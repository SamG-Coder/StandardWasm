namespace CheckedCasts
{
    public static class Conversions
    {
        public static sbyte I32ToI8(int value) => checked((sbyte)value);
        public static byte I32ToU8(int value) => checked((byte)value);
        public static short I32ToI16(int value) => checked((short)value);
        public static ushort I32ToU16(int value) => checked((ushort)value);
        public static int I32ToI32(int value) => checked((int)value);
        public static uint I32ToU32(int value) => checked((uint)value);
        public static long I32ToI64(int value) => checked((long)value);
        public static ulong I32ToU64(int value) => checked((ulong)value);

        public static sbyte U32ToI8(uint value) => checked((sbyte)value);
        public static byte U32ToU8(uint value) => checked((byte)value);
        public static short U32ToI16(uint value) => checked((short)value);
        public static ushort U32ToU16(uint value) => checked((ushort)value);
        public static int U32ToI32(uint value) => checked((int)value);
        public static uint U32ToU32(uint value) => checked((uint)value);
        public static long U32ToI64(uint value) => checked((long)value);
        public static ulong U32ToU64(uint value) => checked((ulong)value);

        public static sbyte I64ToI8(long value) => checked((sbyte)value);
        public static byte I64ToU8(long value) => checked((byte)value);
        public static short I64ToI16(long value) => checked((short)value);
        public static ushort I64ToU16(long value) => checked((ushort)value);
        public static int I64ToI32(long value) => checked((int)value);
        public static uint I64ToU32(long value) => checked((uint)value);
        public static long I64ToI64(long value) => checked((long)value);
        public static ulong I64ToU64(long value) => checked((ulong)value);

        public static sbyte U64ToI8(ulong value) => checked((sbyte)value);
        public static byte U64ToU8(ulong value) => checked((byte)value);
        public static short U64ToI16(ulong value) => checked((short)value);
        public static ushort U64ToU16(ulong value) => checked((ushort)value);
        public static int U64ToI32(ulong value) => checked((int)value);
        public static uint U64ToU32(ulong value) => checked((uint)value);
        public static long U64ToI64(ulong value) => checked((long)value);
        public static ulong U64ToU64(ulong value) => checked((ulong)value);

        public static sbyte F32ToI8(float value) => checked((sbyte)value);
        public static byte F32ToU8(float value) => checked((byte)value);
        public static short F32ToI16(float value) => checked((short)value);
        public static ushort F32ToU16(float value) => checked((ushort)value);
        public static int F32ToI32(float value) => checked((int)value);
        public static uint F32ToU32(float value) => checked((uint)value);
        public static long F32ToI64(float value) => checked((long)value);
        public static ulong F32ToU64(float value) => checked((ulong)value);

        public static sbyte F64ToI8(double value) => checked((sbyte)value);
        public static byte F64ToU8(double value) => checked((byte)value);
        public static short F64ToI16(double value) => checked((short)value);
        public static ushort F64ToU16(double value) => checked((ushort)value);
        public static int F64ToI32(double value) => checked((int)value);
        public static uint F64ToU32(double value) => checked((uint)value);
        public static long F64ToI64(double value) => checked((long)value);
        public static ulong F64ToU64(double value) => checked((ulong)value);
    }

    public static class CastDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
