using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class RuntimeInitializerMetadata
    {
        private const string HelpersNamespace = "System.Runtime.CompilerServices";
        private const string HelpersName = "RuntimeHelpers";

        internal static bool IsRunClassConstructor(byte[] image, int[] layout, int[] heaps, int row, bool definition)
        {
            const string Method = "RunClassConstructor";
            int signature;
            if (definition)
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), Method) ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0) return false;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2, HelpersNamespace, HelpersName))
                    return false;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], Method)) return false;
                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member, HelpersNamespace, HelpersName))
                    return false;
                signature = member[5];
            }

            if (signature <= 0 || signature >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, next);
            int start = next[0];
            // Default static calling convention, one parameter, void, value type.
            if (size < 5 || size > limit - start || U32(image, (uint)start) != 0x11010100)
                return false;
            int end = start + size;
            int parameter = CompressedUnsigned(image, start + 4, end, next);
            return parameter > 0 && next[0] == end &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parameter, "System", "RuntimeTypeHandle");
        }
    }
}
