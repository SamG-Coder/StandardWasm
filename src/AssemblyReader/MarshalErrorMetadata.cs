using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MarshalErrorMetadata
    {
        internal const int GetSystem = 1, SetSystem = 2, GetPInvoke = 3, SetPInvoke = 4;

        internal static bool IsSetter(int member) => (member & 1) == 0;

        internal static int Identify(byte[] image, int[] heaps, int[] methods, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[2], "Marshal") ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System.Runtime.InteropServices")) return 0;
            uint name = (uint)reference[4];
            int member = MatchesAscii(image, heaps, name, "GetLastSystemError") ? GetSystem :
                MatchesAscii(image, heaps, name, "SetLastSystemError") ? SetSystem :
                MatchesAscii(image, heaps, name, "GetLastPInvokeError") ? GetPInvoke :
                MatchesAscii(image, heaps, name, "SetLastPInvokeError") ? SetPInvoke : 0;
            if (member == 0) return 0;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !PlatformAssemblyIdentity.IsMarshalAssembly(image, heaps, methods, counts, reference[AssemblyReferences.AssemblyRow]))
                return 0;
            uint blob = (uint)reference[5], length = (uint)heaps[1];
            uint at = (uint)heaps[0] + blob;
            // Exact static signatures: () -> int32 and (int32) -> void.
            if (IsSetter(member))
                return Fits(blob, 5, length) && U32(image, at) == 0x01010004 && image[at + 4] == 8 ? member : 0;
            return Fits(blob, 4, length) && U32(image, at) == 0x08000003 ? member : 0;
        }
    }
}
