using System;
using FieldLayoutBase;

namespace FieldLayoutDemo
{
    public sealed class Derived : Record
    {
        public static Action SharedCallback;
        public int OwnFirst;
        public static int OwnCount;
        public int OwnLast;

        public Derived(int value) : base(value)
        {
            OwnFirst = unchecked(value + 3);
            OwnLast = unchecked(value + 4);
        }
    }

    public sealed class Initialized
    {
        public static readonly int Marker;
        public int Value;

        static Initialized() => Marker = 41;
        public Initialized(int value) => Value = value;
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var record = new Derived(value);
            record.Last = unchecked(record.Last + 5);
            return unchecked(record.First + record.Last * 3 + record.OwnFirst * Record.Scale + record.OwnLast * 11);
        }

        public static int ReadLocalStatic() => Derived.OwnCount;
        public static int ReadForeignStatic() => Record.SharedCount;
        public static void WriteForeignStatic(int value) => Record.SharedCount = value;
        public static int ConstructInitialized(int value) => new Initialized(value).Value;
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
