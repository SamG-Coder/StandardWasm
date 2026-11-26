using InterfaceContracts;

namespace InterfaceDemo
{
    public sealed class Derived : Token
    {
        public Derived(int value) : base(value) { }
    }

    public sealed class Unrelated
    {
        public int Value;

        public Unrelated(int value) => Value = value;
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var token = new Derived(value);
            var holder = new Holder(token);
            IToken stored = holder.Read();
            if (stored != token) return -1;
            holder.Stored = null;
            if (holder.Read() != null) return -2;
            holder.Stored = token;
            if (holder.Stored != stored) return -3;
            return token.Value;
        }

        public static int Widen(int value)
        {
            var token = new Derived(value);
            return Accept(token, token);
        }

        private static int Accept(IDerivedToken token, Token original)
        {
            var holder = new Holder(token);
            return holder.Read() == original ? original.Value : -1;
        }

        public static int Dispatch(IValue value) => value.Read();
        public static int RejectDispatch() => Dispatch(null);
        public static int RejectCast(int value) => (object)new Unrelated(value) is IToken ? 1 : 0;
        public static Holder Store(int value)
        {
            var holder = new Holder(null);
            holder.Stored = MakeToken(value);
            return holder;
        }

        private static Derived MakeToken(int value) => new Derived(value);
        public static Unrelated MakeUnrelated(int value) => new Unrelated(value);
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
