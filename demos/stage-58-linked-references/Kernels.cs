using ReferenceBridge;

namespace LinkedReferences
{
    public static class Kernels
    {
        public static int Alias(int index, int value)
        {
            var data = new byte[2];
            byte[] alias = Bridge.Edit(data, index, value);
            return data[0] * 100 + data[1] * 10 + alias[0];
        }

        public static int NullArray(byte[] data) => Bridge.Edit(data, 0, 7).Length;

        public static int Signedness(int value)
        {
            var unsigned = new byte[1];
            var signed = new sbyte[1];
            unsigned[0] = unchecked((byte)value);
            signed[0] = unchecked((sbyte)value);
            return Bridge.Unsigned(unsigned) * 1000 + Bridge.Signed(signed);
        }

        public static int ReadLiteral(int index) => Bridge.Literal()[index];

        public static string Label(int value, int variant) => Bridge.MakeLabel(value, variant);

        public static int ReadLabel(int value, int variant, int index) =>
            Bridge.CodeUnit(Label(value, variant), index);

        public static int TextLength(string value) => value.Length;
        public static int TextCodeUnit(string value, int index) => value[index];
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
