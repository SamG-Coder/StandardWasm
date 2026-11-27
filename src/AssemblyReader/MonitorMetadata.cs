using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MonitorMetadata
    {
        internal const int Enter = 1, EnterTaken = 2, Exit = 3;

        internal static int Identify(byte[] image, int[] heaps, int[] methods, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[2], "Monitor") ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System.Threading")) return 0;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !PlatformAssemblyIdentity.IsThreadingAssembly(image, heaps, methods, counts, reference[AssemblyReferences.AssemblyRow]))
                return 0;
            uint blob = (uint)reference[5], at = (uint)heaps[0] + blob, limit = (uint)heaps[1];
            uint name = (uint)reference[4];
            bool enter = MatchesAscii(image, heaps, name, "Enter");
            if (Fits(blob, 5, limit) && U32(image, at) == 0x01010004 && image[at + 4] == 0x1c)
                return enter ? Enter : MatchesAscii(image, heaps, name, "Exit") ? Exit : 0;
            // static void Enter(object, bool&)
            return enter && Fits(blob, 7, limit) && U32(image, at) == 0x01020006 &&
                U16(image, at + 4) == 0x101c && image[at + 6] == 2 ? EnterTaken : 0;
        }
    }
}
