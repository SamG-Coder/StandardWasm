using System.Runtime.InteropServices;

namespace FieldlessExplicitSize
{
    [StructLayout(LayoutKind.Explicit, Size = 60)]
    internal struct Padding
    {
    }

    [StructLayout(LayoutKind.Explicit, Size = 13)]
    internal struct PaddingWithStatic
    {
        internal static int Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Record
    {
        internal int Before;
        internal Padding Gap;
        internal int After;
    }

    internal sealed class Owner
    {
        internal Record Record;
    }

    public static class Probe
    {
        public static unsafe int ExactSizes() => sizeof(Padding) * 100 + sizeof(PaddingWithStatic);

        public static int ManagedCopy()
        {
            Record value = default;
            value.Before = 2;
            value.After = 3;
            var owner = new Owner();
            owner.Record = value;
            value.Before = 5;
            Record snapshot = owner.Record;
            return snapshot.Before * 100 + snapshot.After * 10 + value.Before;
        }

        private static object Box(Padding value) => value;

        public static int NominalBox()
        {
            object first = Box(default);
            object second = Box(default);
            return first is Padding && !(first is PaddingWithStatic) && !ReferenceEquals(first, second) ? 79 : 0;
        }

        public static int StaticField()
        {
            PaddingWithStatic.Count = 83;
            return PaddingWithStatic.Count;
        }

        // This requires the layout of the whole enclosing struct, including
        // addressable padding. The managed field representation cannot do it.
        public static unsafe int EnclosingSize() => sizeof(Record);

        public static unsafe int ByteAddress()
        {
            Padding field = default;
            byte* bytes = (byte*)&field;
            bytes[59] = 89;
            return bytes[59];
        }
    }
}
