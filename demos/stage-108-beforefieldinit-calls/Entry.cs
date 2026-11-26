using BeforeFieldInitDependency;

namespace BeforeFieldInitDemo
{
    public static class Local
    {
        public static int State = Journal.Record(27);
        public static int Skip(int value) => unchecked(value + 1 + Journal.Value);
    }

    public static class Kernels
    {
        public static int Run(int value) => unchecked(Relaxed.Skip(value) + Local.Skip(value));
        public static int Construct(int value) => new RelaxedInstance(value).Read();
        public static int NeedsField() => Relaxed.Read();
        public static int NeedsPrecise(int value) => Precise.Skip(value);
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
