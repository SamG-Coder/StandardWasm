using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class CoreStringEquality
    {
        // The managed callers check that both strings are non-null and of
        // equal length before entering this private storage helper. The
        // byte count is exactly twice the code-unit count, so equality of
        // those byte ranges is equality of the tracked UTF-16 code units.
        // Neither interior address is stored, offset, or returned.
        internal static bool Lower(byte[] image, int[] layout, int[] heaps, int method, int[] body, int[] records)
        {
            if (body[1] != 27 || body[2] < 3 || body[3] != 0 || body[5] != 0 ||
                (MethodDefinitions.ReadColumn(image, layout, method, 2) & 7) != 1 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "EqualsHelper"))
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, method,
                    "System", "String", "EqualsHelper", new byte[] { 0, 2, 2, 0x0e, 0x0e }, true))
                return false;

            int start = body[0];
            // ldarg.0; callvirt raw; ldarg.1; callvirt raw; ldarg.0;
            // callvirt length; ldc.i4.2; mul; conv.u; call sequenceEqual; ret.
            if (U16(image, (uint)start) != 0x6f02 ||
                U16(image, (uint)(start + 6)) != 0x6f03 ||
                U16(image, (uint)(start + 12)) != 0x6f02 ||
                U32(image, (uint)(start + 18)) != 0x28e05a18 || image[start + 26] != 0x2a)
                return false;

            uint raw = U32(image, (uint)(start + 2));
            uint length = U32(image, (uint)(start + 14));
            uint comparison = U32(image, (uint)(start + 22));
            if (raw != U32(image, (uint)(start + 8)) || raw >> 24 != 6 || length >> 24 != 6 ||
                comparison >> 24 != 6 ||
                !IsRawStringData(image, layout, heaps, counts, first, (int)(raw & 0xffffff)) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, (int)(length & 0xffffff),
                    "System", "String", "get_Length", new byte[] { 0x20, 0, 8 }, false) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, (int)(comparison & 0xffffff),
                    "System", "SpanHelpers", "SequenceEqual", new byte[] { 0, 3, 2, 0x10, 5, 0x10, 5, 0x19 }, true))
                return false;

            Record(records, 0, Load, 0, 6);
            Record(records, 6, Load, 1, 21);
            Record(records, 21, StringEquals, 0, 26);
            Record(records, 26, Return, 0, 27);
            return true;
        }

        private static bool IsRawStringData(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int method)
        {
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, method,
                "System", "String", "GetRawStringDataAsUInt8", new byte[] { 0x20, 0, 0x10, 5 }, false))
                return false;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, method, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 12 || body[2] < 1 ||
                body[3] != 0 || body[5] != 0)
                return false;

            int start = body[0];
            uint field = U32(image, (uint)(start + 2));
            return U16(image, (uint)start) == 0x7c02 && image[start + 6] == 0x28 && image[start + 11] == 0x2a &&
                field >> 24 == 4 && field != 0x04000000 && (field & 0xffffff) <= (uint)counts[4] &&
                StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, (int)(field & 0xffffff)) &&
                CoreStringRange.IsClosedMethod(image, layout, heaps, counts, first, U32(image, (uint)(start + 7)),
                    "System.Runtime.CompilerServices", "Unsafe", "As", new byte[] { 3, 5 },
                    new byte[] { 0x10, 2, 1, 0x10, 0x1e, 1, 0x10, 0x1e, 0 });
        }

        private static void Record(int[] records, int offset, int kind, int operand, int next)
        {
            int slot = offset * Width;
            records[slot] = kind;
            records[slot + 1] = operand;
            records[slot + 2] = next;
        }
    }
}
