using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ConstraintMarkers
    {
        internal static bool IsValueType(byte[] image, int[] layout, int[] heaps, int coded)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return start >= 0 && ObjectLayouts.IsPlatformValueType(image, heaps, layout, counts, start, coded);
        }

        internal static bool IsUnmanaged(byte[] image, int[] layout, int[] heaps, int coded)
        {
            if ((coded & 3) != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadType(image, heaps, layout, coded >> 2, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0) return false;
            uint name = (uint)reference[2], space = (uint)reference[3], size = (uint)heaps[3];
            if (!Fits(name, 14, size) || !Fits(space, 31, size)) return false;
            uint n = (uint)heaps[2] + name, s = (uint)heaps[2] + space;
            // CLR metadata uses System.Runtime.InteropServices.UnmanagedType
            // as the required modifier on the ValueType constraint.
            if (U32(image, n) != 0x616d6e55 || U32(image, n + 4) != 0x6567616e ||
                U32(image, n + 8) != 0x70795464 || U16(image, n + 12) != 0x65 ||
                U32(image, s) != 0x74737953 || U32(image, s + 4) != 0x522e6d65 ||
                U32(image, s + 8) != 0x69746e75 || U32(image, s + 12) != 0x492e656d ||
                U32(image, s + 16) != 0x7265746e || U32(image, s + 20) != 0x6553706f ||
                U32(image, s + 24) != 0x63697672 || U16(image, s + 28) != 0x7365 || image[s + 30] != 0)
                return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts, reference[AssemblyReferences.AssemblyRow]);
        }
    }
}
