using System;
using VirtualDispatchDependency;

namespace VirtualDispatchDemo
{
    public sealed class Declared : Intermediate
    {
        public override int Read(int value) => unchecked(base.Read(value) + 17);
    }

    public static class Kernels
    {
        public static int RunInherited(int value) => ((Root)new Inherited()).Read(value);
        public static int RunDeclared(int value) => new Declared().Read(value);
        public static int RunHiddenBase(int value) => ((Root)new HiddenLeaf()).Read(value);
        public static int RunIntroducedSlot(int value) => ((Hiding)new HiddenLeaf()).Read(value);

        public static int RunNull(int marker)
        {
            try { return ReadInherited(marker == 0 ? null : new Inherited(), marker); }
            catch (NullReferenceException) { return -123; }
        }

        private static int ReadInherited(Inherited receiver, int value) => receiver.Read(value);
        public static int RejectOpen(Root receiver) => receiver.Read(9);
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
