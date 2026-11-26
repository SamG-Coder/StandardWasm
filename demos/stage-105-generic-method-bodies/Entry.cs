using GenericBodyConsumer;
using GenericBodyDefinition;

namespace GenericBodyDemo
{
    public static class Kernels
    {
        public static int Run(int value) => Lifecycle(value) ^ Shared(value) ^ Narrow(value);

        // int and uint share i32 storage. Division and comparison semantics come
        // from the caller's div/div.un and blt/blt.un, not from the holder.
        public static int SignedQuotient(int left, int right) =>
            new Holder<int>(left).Get() / Holder<int>.Create(right).Get();

        public static int UnsignedQuotient(int left, int right) =>
            unchecked((int)(Consumer.MakeUnsigned(unchecked((uint)left)).Get() / new Holder<uint>(unchecked((uint)right)).Get()));

        public static int Compare(int left, int right)
        {
            var signed = new Holder<int>(left);
            var unsigned = new Holder<uint>(unchecked((uint)left));
            int result = signed.Get() < right ? 1 : 0;
            if (unsigned.Get() < unchecked((uint)right)) result |= 2;
            if (signed.Get() > right) result |= 4;
            if (unsigned.Get() > unchecked((uint)right)) result |= 8;
            return result;
        }

        // sbyte and byte have distinct packed storage and extension.
        public static int Narrow(int value)
        {
            var signed = new Holder<sbyte>(unchecked((sbyte)value));
            var unsigned = new Holder<byte>(unchecked((byte)value));
            int previous = signed.Swap(unchecked((sbyte)(value >> 8)));
            return previous * 1000 + unsigned.Get() * 3 + signed.Get();
        }

        public static int Lifecycle(int value)
        {
            Holder<int> holder = Holder<int>.Create(value);
            int previous = holder.Swap(unchecked(value + 1));
            holder.Set(unchecked(holder.Get() * 3));
            Holder<int> copy = holder.Copy();
            copy.Set(previous);
            int picked = Holder<int>.Pick(value < 0, holder, copy);
            return unchecked(picked * 31 + Consumer.ReadSigned(copy) + holder.Writes * 7 + copy.Writes);
        }

        public static int Shared(int value)
        {
            Holder<uint> holder = Consumer.MakeUnsigned(unchecked((uint)value));
            int writes = Consumer.Halve(holder);
            return unchecked((int)holder.Get() * 4 + writes);
        }

        public static int Text(int value) => new Holder<string>(value < 0 ? null : "abc").Get().Length + value;

        public static int GenericMethod(int value) => new Holder<int>(value).Echo(value);

        public static int Virtual(int value) => new Holder<int>(value).Describe();
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
