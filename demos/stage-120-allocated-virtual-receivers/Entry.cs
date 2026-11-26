namespace AllocatedReceiverDemo
{
    public class Root
    {
        public virtual int Read() => 100;
    }

    public class Intermediate : Root
    {
        public Intermediate Next;
        protected int Seed;

        public Intermediate(int seed) { Seed = seed; }

        public override int Read() => unchecked(Seed + 20);
        public Intermediate Replace() => new Leaf(Seed);
    }

    public sealed class Leaf : Intermediate
    {
        public Leaf(int seed) : base(seed) { }
        public override int Read() => unchecked(Seed + 40);
    }

    public class Configurable : Root
    {
        private int seed;
        public virtual int Value { set { seed = value; } }
        public override int Read() => unchecked(seed + 20);
    }

    public static class Kernels
    {
        public static int Direct(int input) => ((Root)new Intermediate(input)).Read();
        public static int Initializer(int input) => new Configurable { Value = input }.Read();
        public static int SameClassJoin(int input) =>
            (input > 0 ? new Intermediate(input) : new Intermediate(unchecked(input + 2))).Read();

        public static int Unknown(Intermediate receiver) => receiver.Read();
        public static int MixedJoin(Intermediate receiver, int input) =>
            (input > 0 ? new Intermediate(input) : receiver).Read();
        public static int ReplacementCall(int input) => new Intermediate(input).Replace().Read();
        public static int ReplacementField(int input) =>
            new Intermediate(input) { Next = new Leaf(input) }.Next.Read();
        public static int ReplacementElement(int input) => new Intermediate[] { new Leaf(input) }[0].Read();
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
