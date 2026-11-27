using PeLibrary;

namespace PortableCompiler
{
    // Issue #304: CoreLib's generated SR properties are `get_X => SR.GetResourceString("X")`,
    // which reaches ResourceManager (not compilable). When CoreLib's own
    // System.SR::GetResourceString(string) is called directly after a literal key, the pair is
    // replaced by the key's string in CoreLib's embedded neutral resources
    // (System.Private.CoreLib.Strings.resources). These are the strings the CLR returns when no
    // satellite resources apply, as for an invariant or English UI culture. Any other call,
    // and a key that is not a string resource, keeps the ordinary call and its diagnostic.
    internal static class ResourceStringFolding
    {
        private const string ResourceName = "System.Private.CoreLib.Strings.resources";

        // Returns 1 when the call at slot was folded, 0 when it is not a foldable call, or a
        // negative status.
        internal static int FoldResourceCall(byte[] image, int[] heaps, int[] layout, int[] records, int slot, int[] clauses,
            byte[] literals, int[] literalSize)
        {
            int target = records[slot + 1];
            if (!NamedGetResourceString(image, heaps, layout, target)) return 0;
            int key = StraightLinePredecessor(records, slot);
            if (key < 0 || records[key] != BranchIl.LoadString) return 0;
            // Adjacency is not enough: a branch, switch or handler boundary reaching the call (or the
            // ldstr's successor) could bring a different key on the stack. Fold only when the call is
            // entered solely by falling through from the literal.
            if (ValueOperands.Entry(records, slot / BranchIl.Width, clauses)) return 0;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            if (ObjectLayouts.Owner(image, heaps, layout, target, false) !=
                ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "SR")) return 0;
            // static string GetResourceString(string)
            int blob = MethodDefinitions.ReadColumn(image, layout, target, 4);
            if (blob <= 0 || blob >= heaps[1]) return 0;
            int at = heaps[0] + blob;
            if (image[at] != 4 || image[at + 1] != 0 || image[at + 2] != 1 || image[at + 3] != 0x0e || image[at + 4] != 0x0e)
                return 0;

            var range = new int[2];
            var name = new byte[ResourceName.Length];
            for (int i = 0; i < name.Length; i++) name[i] = (byte)ResourceName[i];
            if (ManifestResources.Find(image, heaps, layout, name, range) < 0) return 0;

            // The key is already in the literal pool (LoadString links before this call).
            int keyStart = records[key + 1], keyLength = records[key + 3];
            var keyUnits = new int[keyLength];
            for (int i = 0; i < keyLength; i++)
                keyUnits[i] = literals[keyStart + 2 * i] | (literals[keyStart + 2 * i + 1] << 8);
            var value = new int[8192];
            int units = ResourceStrings.Find(image, range[0], range[1], keyUnits, keyLength, value);
            if (units == -6) return 0;
            if (units < 0) return units;

            int start = literalSize[0];
            if (units > (literals.Length - start) / 2) return -3;
            for (int i = 0; i < units; i++)
            {
                literals[start + 2 * i] = (byte)value[i];
                literals[start + 2 * i + 1] = (byte)(value[i] >> 8);
            }
            literalSize[0] = start + 2 * units;
            records[key + 1] = start;
            records[key + 3] = units;
            // [String key] -> [String value] becomes [String value]: the call is now a no-op.
            records[slot] = BranchIl.Nop;
            records[slot + 1] = 0;
            records[slot + 3] = 0;
            return 1;
        }

        private static bool NamedGetResourceString(byte[] image, int[] heaps, int[] layout, int target) =>
            ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, target, 3), "GetResourceString");

        // The record whose next instruction is slot: its immediate straight-line predecessor.
        private static int StraightLinePredecessor(int[] records, int slot)
        {
            int pc = slot / BranchIl.Width;
            for (int p = slot - BranchIl.Width; p >= 0; p -= BranchIl.Width)
                if (records[p] != 0) return records[p + 2] == pc ? p : -1;
            return -1;
        }
    }
}
