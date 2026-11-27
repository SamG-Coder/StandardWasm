using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MonitorMetadata
    {
        internal const int Enter = 1, EnterTaken = 2, Exit = 3;

        internal static int Identify(byte[] image, int[] heaps, int[] methods, int row, bool member)
        {
            uint name, blob;
            var counts = new int[45];
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                    reference[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[2], "Monitor") ||
                    !MatchesAscii(image, heaps, (uint)reference[3], "System.Threading") ||
                    MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                    !PlatformAssemblyIdentity.IsThreadingAssembly(image, heaps, methods, counts,
                        reference[AssemblyReferences.AssemblyRow])) return 0;
                name = (uint)reference[4];
                blob = (uint)reference[5];
            }
            else
            {
                // Original CoreLib callers use a MethodDef in the same image.
                name = (uint)MethodDefinitions.ReadColumn(image, methods, row, 3);
                if (!MatchesAscii(image, heaps, name, "Enter") && !MatchesAscii(image, heaps, name, "Exit"))
                    return 0;
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0 || row < 1 || row > counts[6] ||
                    (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x10) == 0 ||
                    !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return 0;
                int type = ObjectLayouts.Owner(image, heaps, methods, row, false);
                if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, type, "System.Threading", "Monitor"))
                    return 0;
                blob = (uint)MethodDefinitions.ReadColumn(image, methods, row, 4);
            }
            uint at = (uint)heaps[0] + blob, limit = (uint)heaps[1];
            bool enter = MatchesAscii(image, heaps, name, "Enter");
            if (Fits(blob, 5, limit) && U32(image, at) == 0x01010004 && image[at + 4] == 0x1c)
                return enter ? Enter : MatchesAscii(image, heaps, name, "Exit") ? Exit : 0;
            // static void Enter(object, bool&)
            return enter && Fits(blob, 7, limit) && U32(image, at) == 0x01020006 &&
                U16(image, at + 4) == 0x101c && image[at + 6] == 2 ? EnterTaken : 0;
        }
    }
}
