using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class FrameworkFacadeVersions
    {
        // The caller supplies its chosen framework pack. Identity checks do
        // not authenticate those files. Allow only the shipped netstandard
        // 2.1 pure facade to satisfy the older 2.0 contract; normal public-key
        // and neutral-culture matching still runs after this version check.
        internal static bool AllowsNetStandard(byte[] source, int[] heaps, int[] methods, int[] reference,
            byte[] candidate, uint definition, int[] candidateCounts)
        {
            if (candidateCounts[2] != 1 || candidateCounts[4] != 0 || candidateCounts[6] != 0 ||
                candidateCounts[39] == 0) return false;
            uint version = (uint)reference[0];
            if (U32(source, version) != 2 || U32(source, version + 4) != 0 ||
                U32(candidate, definition + 4) != 0x00010002 || U32(candidate, definition + 8) != 0)
                return false;

            var counts = new int[45];
            return MetadataTableRows.ReadCounts(source, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(source, heaps, methods, counts,
                    reference[AssemblyReferences.AssemblyRow], netstandardOnly: true);
        }
    }
}
