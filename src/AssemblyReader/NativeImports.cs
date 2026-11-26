using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class NativeImports
    {
        // Return the unique MethodDef imported under this module and entry name.
        // The managed method name can differ, particularly in generated stubs.
        internal static int Find(byte[] image, int[] heaps, int[] methods, int[] counts, string module, string entry)
        {
            if (counts[28] == 0) return 0;
            int map = MetadataTableRows.TableStart(heaps, methods, counts, 28);
            int modules = MetadataTableRows.TableStart(heaps, methods, counts, 26);
            if (map < 0 || modules < 0) return -1;
            int strings = methods[2];
            int memberWidth = counts[4] < 32768 && counts[6] < 32768 ? 2 : 4;
            int moduleWidth = counts[26] < 65536 ? 2 : 4;
            int width = 2 + memberWidth + strings + moduleWidth;
            int found = 0;
            for (int i = 0; i < counts[28]; i++)
            {
                uint at = (uint)(map + i * width);
                uint name = Index(image, at + (uint)(2 + memberWidth), strings);
                if (!MatchesAscii(image, heaps, name, entry)) continue;
                uint scope = Index(image, at + (uint)(2 + memberWidth + strings), moduleWidth);
                if (scope == 0 || scope > (uint)counts[26]) return -1;
                uint scopeName = Index(image, (uint)(modules + ((int)scope - 1) * strings), strings);
                if (!MatchesAscii(image, heaps, scopeName, module)) continue;

                uint member = Index(image, at + 2, memberWidth);
                int row = (int)(member >> 1);
                // MemberForwarded tag 1 identifies MethodDef. A matching field
                // or duplicate mapping cannot identify the managed call target.
                if ((member & 1) != 1 || row < 1 || row > counts[6] || found != 0) return -1;
                if (MethodDefinitions.ReadColumn(image, methods, row, 0) != 0 ||
                    (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x2010) != 0x2010)
                    return -1;
                found = row;
            }
            return found;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
