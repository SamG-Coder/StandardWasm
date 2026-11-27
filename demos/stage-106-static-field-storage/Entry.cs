using System;
using StaticStorageDependency;

namespace StaticStorageDemo
{
    public static class Kernels
    {
        public static int Counter;

        [ThreadStatic]
        private static object threadKey;

        public static int Defaults() =>
            Counter == 0 && Storage.Counter == 0 && Storage.Narrow == 0 && Storage.Wide == 0 &&
            Storage.Single == 0f && Storage.Double == 0d && Storage.Key == null && Storage.Token == null ? 1 : -1;

        public static int Run(int input)
        {
            Counter = unchecked(input + 1);
            Storage.Counter = unchecked(input + 2);
            Storage.Narrow = unchecked((short)(input + 65535));
            Storage.Wide = input;
            Storage.Single = 1.25f;
            Storage.Double = 2.5d;
            var key = new Token { Value = input };
            Storage.Token = key;
            Storage.Key = key;
            if (Storage.Token != key || Storage.Key != key || Storage.Wide != input ||
                Storage.Single != 1.25f || Storage.Double != 2.5d) return -1;
            Storage.Key = null;
            return Storage.Key == null ? unchecked(Counter + Storage.Counter + Storage.Narrow + Storage.Token.Value) : -2;
        }

        public static int Step(int delta)
        {
            Counter = unchecked(Counter + 1);
            int shared = Storage.Add(delta);
            return Storage.Counter == shared ? unchecked(Counter * 100 + shared) : -1;
        }

        public static int WithEventArgs(int delta)
        {
            Counter = delta;
            EventArgs first = EventArgs.Empty;
            EventArgs second = EventArgs.Empty;
            return first == second ? Counter : -1;
        }

        public static int ReadAnnotatedKey() => AnnotatedKeys.Read();

        public static int RejectInitialized() => Initialized.Counter;
        public static int ThreadState(int delta)
        {
            int firstAccess = threadKey == null ? 1000 : 0;
            if (threadKey == null) threadKey = new object();
            Storage.ThreadCounter = unchecked(Storage.ThreadCounter + delta);
            return unchecked(firstAccess + Storage.ThreadCounter);
        }

        public static int ReadCompilerGenerated() => AttributedStorage.Counter;
        public static int ReadUnsupportedAttribute() => AttributedStorage.UnsupportedCounter;
    }

    internal static class AttributedStorage
    {
        [System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Counter = 0;

        [UnmappedField]
        internal static int UnsupportedCounter = 0;
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    internal sealed class UnmappedFieldAttribute : System.Attribute
    {
    }

    public static class AnnotatedKeys
    {
#nullable enable annotations
        private static readonly object? Key = new object();
#nullable disable annotations
        private static readonly object Other = new object();

        public static int Read()
        {
            var first = Key;
            return first != null && first == Key && first != Other ? 1 : 0;
        }
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
