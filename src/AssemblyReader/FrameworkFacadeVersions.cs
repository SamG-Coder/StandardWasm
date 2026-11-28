using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class FrameworkFacadeVersions
    {
        // The selected pure facade may satisfy a known older framework contract.
        // The normal public-key and neutral-culture checks still apply.
        internal static bool Allows(byte[] source, int[] heaps, int[] methods, int[] reference,
            byte[] candidate, uint definition, int[] candidateCounts)
        {
            if (candidateCounts[2] != 1 || candidateCounts[4] != 0 || candidateCounts[6] != 0 ||
                candidateCounts[39] == 0) return false;
            uint version = (uint)reference[0];
            if (U32(source, version + 4) != 0 || U32(candidate, definition + 8) != 0)
                return false;
            uint requested = U32(source, version), selected = U32(candidate, definition + 4);
            bool netstandard = requested == 2 && selected == 0x00010002;
            bool runtime = requested == 9 && selected == 10 &&
                MatchesAscii(source, heaps, (uint)reference[1], "System.Runtime");
            if (!netstandard && !runtime) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(source, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(source, heaps, methods, counts,
                    reference[AssemblyReferences.AssemblyRow], netstandardOnly: netstandard);
        }
    }
}
