using GenericBaseDependency;

namespace GenericBaseDemo
{
    public class LocalBase<T>
    {
        public T Item;
        public LocalBase(T value) { Item = value; }
    }

    public sealed class LocalInt : LocalBase<int>
    {
        public LocalInt(int value) : base(value) { }
    }

    public sealed class LinkedInt : Forward<int>
    {
        public LinkedInt Next;
        public LinkedInt(int value) : base(value) { }
        public override int Code() => 23;
    }

    public sealed class LinkedSibling : Box<int>
    {
        public LinkedSibling(int value) : base(value) { }
    }

    public sealed class LinkedBool : Forward<bool>
    {
        public LinkedBool(bool value) : base(value) { }
    }

    public sealed class NestedInt : Nested<int>
    {
        public NestedInt(int[] value) : base(value) { }
    }

    public sealed class Holder
    {
        public LinkedHolder Other;
    }

    public sealed class LinkedHolder : LocalBase<Holder>
    {
        public LinkedHolder() : base(null) { }
    }

    public sealed class SelfTyped : LocalBase<SelfTyped>
    {
        public SelfTyped() : base(null) { }
    }

    public static class Kernels
    {
        public static int Local(int value)
        {
            LocalBase<int> reference = new LocalInt(value);
            reference.Item = unchecked(value + 4);
            return reference.Item;
        }

        private static int Read(Box<int> item) => item.Value;
        private static int ReadCode(Root item) => item.Code();

        public static int Linked(int value)
        {
            var item = new LinkedInt(value);
            item.Next = item;
            item.Value = unchecked(value + 7);
            return Read(item.Next);
        }

        public static int ReusedAndDistinct(int value)
        {
            var first = new LinkedInt(value);
            var second = new LinkedSibling(2);
            var flag = new LinkedBool(value > 0);
            flag.Value = !flag.Value;
            return unchecked(Read(first) + Read(second) + (flag.Value ? 1 : 0));
        }

        public static int RootVirtual(int value)
        {
            if (value < 0) return ReadCode(new LinkedSibling(value));
            return ReadCode(new LinkedInt(value));
        }

        public static int NestedArgument(int value) => new NestedInt(new int[] { value }).Value[0];

        public static int ReentrantArgument(int value)
        {
            var item = new LinkedHolder();
            item.Item = new Holder { Other = item };
            return item.Item.Other == item ? value : 0;
        }

        public static int RecursiveTypeArgument() => new SelfTyped().Item == null ? 1 : 0;
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
