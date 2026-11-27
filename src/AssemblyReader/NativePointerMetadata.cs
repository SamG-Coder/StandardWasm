using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class NativePointerMetadata
    {
        internal static bool IsPointerConstructor(byte[] image, int[] heaps, int[] methods, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System") ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;

            uint name = (uint)reference[2];
            if (!MatchesAscii(image, heaps, name, "IntPtr") && !MatchesAscii(image, heaps, name, "UIntPtr"))
                return false;
            uint blob = (uint)reference[5];
            if (!Fits(blob, 6, (uint)heaps[1])) return false;
            uint at = (uint)heaps[0] + blob;
            // Exact instance .ctor(void*) signature. Other constructors perform numeric conversions.
            if (U32(image, at) != 0x01012005 || U16(image, at + 4) != 0x010f) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts, reference[AssemblyReferences.AssemblyRow]);
        }
    }
}
