using System;

namespace NativeWidthConsumer
{
    // Each width-dependent method calls an original CoreLib member whose IL
    // depends on the native width that CoreLib was built for.
    public static class Entry
    {
        public static int Size() => IntPtr.Size;
        public static int UnsignedSize() => UIntPtr.Size;
        public static nint SignedMax() => IntPtr.MaxValue;
        public static nint SignedMin() => IntPtr.MinValue;
        public static nuint UnsignedMax() => UIntPtr.MaxValue;
        public static long WidenedSignedMax() => (long)IntPtr.MaxValue;
        public static long WidenedUnsignedMax() => (long)UIntPtr.MaxValue;

        // Uses CoreLib only for the System.Object root and its empty
        // constructor, so it stays valid against a 64-bit CoreLib.
        public static int Allocate() => new Box(41).Next();
    }

    public sealed class Box
    {
        private readonly int value;
        public Box(int value) { this.value = value; }
        public int Next() => value + 1;
    }
}
