using ReadContract;

namespace InterfaceDispatchDemo
{
    public interface IAlternate
    {
        int Read();
    }

    public abstract class Resource : IReadable
    {
        public int Read() => ReadCore();
        protected abstract int ReadCore();
    }

    public sealed class FileResource : Resource
    {
        protected override int ReadCore() => 21;
    }

    public sealed class MemoryResource : Resource
    {
        protected override int ReadCore() => 42;
    }

    public sealed class HiddenResource : Resource
    {
        public new int Read() => 99;
        protected override int ReadCore() => 31;
    }

    public sealed class RemappedResource : Resource, IReadable
    {
        public new int Read() => 54;
        protected override int ReadCore() => 8;
    }

    public sealed class ExplicitResource : Resource, IReadable, IAlternate
    {
        int IReadable.Read() => 63;
        int IAlternate.Read() => 79;
        protected override int ReadCore() => 7;
    }

    public sealed class AlternateResource : Resource, IAlternate
    {
        int IAlternate.Read() => 95;
        protected override int ReadCore() => 91;
    }

    public static class Kernels
    {
        private static int Read(IReadable resource) => resource.Read();

        private static Resource Choose(int choice)
        {
            if (choice < 0) return null;
            if (choice == 0) return new FileResource();
            if (choice == 1) return new MemoryResource();
            if (choice == 2) return new HiddenResource();
            if (choice == 3) return new RemappedResource();
            if (choice == 4) return new ExplicitResource();
            return new AlternateResource();
        }

        public static int ReadResource(int choice) => Read(Choose(choice));

        public static int ReadAlternate() => ((IAlternate)new ExplicitResource()).Read();
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
