using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class ReadOnlySpanCasts
    {
        // Same-width Char to Int16 keeps the original packed owner, start and
        // count. Match the actual read-only overload and both exact CLI types.
        internal static bool IsCharToInt16(byte[] image, int[] layout, int[] heaps, int[] counts, int[] spec)
        {
            int argument = spec[MethodSpecifications.Arguments];
            if (spec[MethodSpecifications.Arity] != 2 || spec[MethodSpecifications.End] != argument + 2 ||
                image[argument] != 3 || image[argument + 1] != 6) return false;
            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int signature;
            if ((target & 1) == 0)
            {
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                    !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner,
                        "System.Runtime.InteropServices", "MemoryMarshal") ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Cast"))
                    return false;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], "Cast") ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                        "System.Runtime.InteropServices", "MemoryMarshal", includeMemory: true)) return false;
                signature = member[5];
            }

            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            if (signature <= 0 || signature >= heaps[1]) return false;
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, cursor);
            if (size < 15 || size > limit - cursor[0]) return false;
            int end = cursor[0] + size;
            return SignatureReader.TakeElement(image, cursor, end, 0x10) &&
                SignatureReader.TakeElement(image, cursor, end, 2) &&
                SignatureReader.TakeElement(image, cursor, end, 1) &&
                IsReadOnlyParameter(image, layout, heaps, cursor, end, 1) &&
                IsReadOnlyParameter(image, layout, heaps, cursor, end, 0) &&
                cursor[0] == end;
        }

        private static bool IsReadOnlyParameter(byte[] image, int[] layout, int[] heaps, int[] cursor, int end, int parameter)
        {
            int span = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
            return span > 0 && SpanMetadata.IsDefinition(image, layout, heaps, span) &&
                SignatureReader.TakeElement(image, cursor, end, 0x1e) &&
                SignatureReader.TakeElement(image, cursor, end, parameter);
        }
    }
}
