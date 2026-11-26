using System;
using DispatchDependency;

namespace RuntimeDispatchDemo
{
    public static class Kernels
    {
        private static Item Choose(int input)
        {
            if (input < 0) return null;
            if (input == 0) return new Item();
            if (input == 1) return new DerivedItem();
            return new HiddenLeaf();
        }

        private static int Read(Item item, int input) => item.Read(input);
        private static int ReadHidden(HiddenItem item, int input) => item.Read(input);
        private static Item Build(Maker maker) => maker.Build();

        public static int LinkedProperty(int input)
        {
            try { return Read(new Provider(Choose(input)).Current, input); }
            catch (NullReferenceException) { return -123; }
        }

        public static int NullOnly(int input)
        {
            try { return Read(null, input); }
            catch (NullReferenceException) { return -123; }
        }

        public static int HiddenSlot(int input) => ReadHidden(new HiddenLeaf(), input);

        // The only allocation of LateItem is inside a virtual override
        // discovered after the call to Read has already been linked.
        public static int DiscoveredOverride(int input) => Read(Build(new LateMaker()), input);
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
