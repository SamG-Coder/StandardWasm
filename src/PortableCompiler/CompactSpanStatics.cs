using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class CompactSpanStatics
    {
        // These static methods have a compact declaring context but no
        // receiver. The ordinary managed decoder and verifier compile the IL.
        internal static int Resolve(BundleImages bundle, byte[] caller, int[] layout, int[] heaps,
            int owner, int[] reference, int[] result)
        {
            if (!MatchesAscii(caller, heaps, (uint)reference[4], "get_Empty")) return -2;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(caller, heaps, reference[5], 0, cursor);
            if (end < 0) return -2;
            int definition = SignatureReader.ReadGenericValue(caller, cursor, end, 0);
            if (definition <= 0 || cursor[0] != end || !SpanMetadata.IsDefinition(caller, layout, heaps, definition))
                return -2;

            int parent = reference[AssemblyReferences.GenericParent];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(caller, heaps[0] + parent, limit, cursor);
            int start = cursor[0];
            if (length <= 0 || length > limit - start) return -1;
            int declaring = SignatureReader.ReadSingleArgumentValue(caller, cursor, start + length);
            if (declaring <= 0) return declaring;
            var identity = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, caller, layout, heaps, owner, declaring, identity);
            if (status < 0) return status;
            if (identity[0] < 0 || identity[1] <= 0) return -2;
            var returned = new int[2];
            status = AssemblyBundle.ClassIdentity(bundle, caller, layout, heaps, owner, definition, returned);
            if (status < 0) return status;
            if (identity[0] != returned[0] || identity[1] != returned[1]) return -2;
            byte[] image = bundle.Get(identity[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, targetLayout, targetHeaps) < 0) return -1;
            var methods = new int[2];
            status = ObjectLayouts.MethodRange(image, targetHeaps, targetLayout, identity[1], methods);
            if (status < 0) return status;
            int found = 0;
            for (int row = methods[0]; row < methods[1]; row++)
            {
                if (!IsDefinition(image, targetLayout, targetHeaps, identity[1], row)) continue;
                if (found != 0) return -5;
                found = row;
            }
            if (found == 0) return -2;
            result[0] = identity[0];
            result[1] = found;
            result[3] = SignatureTypes.CharacterSpan;
            return 0;
        }

        internal static bool IsDefinition(byte[] image, int[] layout, int[] heaps, int owner, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2c57) != 0x0816 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "get_Empty") ||
                !SpanMetadata.IsDefinition(image, layout, heaps, owner << 2))
                return false;
            var cursor = new int[1];
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int end = SignatureReader.ReadStatic(image, heaps, signature, 0, cursor);
            return end >= 0 && SignatureReader.ReadGenericValue(image, cursor, end, 0) == owner << 2 && cursor[0] == end;
        }
    }
}
