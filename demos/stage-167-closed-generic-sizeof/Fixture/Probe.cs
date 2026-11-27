using System.Runtime.InteropServices;

namespace ClosedGenericSize
{
    public static unsafe class Probe
    {
        private enum Small : byte { Zero }
        private enum Wide : long { Zero }

        [StructLayout(LayoutKind.Sequential)]
        private struct Aligned
        {
            internal byte Prefix;
            internal long Value;
        }

        private struct Empty { }

        public static int Narrow() => Size<bool>() * 1000 + Size<byte>() * 100 + Size<char>() * 10 + Size<short>();
        public static int WideValues() => Size<int>() * 1000 + Size<long>() * 100 + Size<float>() * 10 + Size<double>();
        public static int Native() => Size<nint>() * 10 + Size<nuint>();
        public static int EnumValues() => Size<Small>() * 10 + Size<Wide>();
        public static int Sequential() => Size<Aligned>() * 10 + Size<Empty>();
        public static int DeclaringType() => new Cell<char>().Read() * 10 + new Cell<long>().Read();

        private static int Size<T>() where T : unmanaged => sizeof(T);

        private struct Cell<T> where T : unmanaged
        {
            internal int Read() => sizeof(T);
        }
    }
}
