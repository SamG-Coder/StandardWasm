using System.Runtime.InteropServices;

namespace FieldlessSequentialValue
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct Empty
    {
        public static int Marker;
    }

    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct OtherEmpty { }

    [StructLayout(LayoutKind.Sequential, Size = 1, Pack = 1)]
    public struct PackedEmpty { }

    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct SizedByte
    {
        public byte Value;
    }

    public struct Carrier
    {
        public Empty Marker;
        public int Value;
    }

    public sealed class Holder<T>
    {
        public T Marker;
        public int Value;

        public Holder(T marker, int value)
        {
            Marker = marker;
            Value = value;
        }
    }

    public static class Kernels
    {
        private static Empty Copy(Empty value) => value;
        private static int Use(Empty value, int code) => code;
        private static int UsePacked(PackedEmpty value) => 4;
        private static int UseSized(SizedByte value) => 5;

        public static int EmptyLocal() => Use(Copy(default(Empty)), 16);

        public static int ContainingCopy()
        {
            var original = default(Carrier);
            original.Value = 20;
            Carrier clone = original;
            original.Value = 3;
            return clone.Value - original.Value;
        }

        public static int GenericArgument() => new Holder<Empty>(default(Empty), 23).Value;

        public static int BothGenericArguments()
        {
            var first = new Holder<Empty>(default(Empty), 13);
            var second = new Holder<OtherEmpty>(default(OtherEmpty), 17);
            return first.Value + second.Value;
        }

        private static int Make(int value) => value + 40;

        public static int StoreCallResult(int value)
        {
            var target = default(Carrier);
            target.Value = Make(value);
            return target.Value;
        }

        public static int StoreNestedCallResult(int value)
        {
            var target = default(Carrier);
            target.Value = 2 + Make(Make(value));
            return target.Value;
        }

        private static void WriteByReference(ref Carrier value) => value.Value = 42;

        public static int WriteThroughBorrow()
        {
            var target = default(Carrier);
            WriteByReference(ref target);
            return target.Value;
        }

        public static int RejectPacked() => UsePacked(default(PackedEmpty));

        public static int RejectNonempty() => UseSized(default(SizedByte));
    }
}
