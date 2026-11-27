using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class CoreStringSearch
    {
        // CoreLib passes a short reference into the desktop string layout.
        // Match the complete trusted storage operation before using the Wasm
        // string's own UTF-16 backing array. The original managed caller stays intact.
        internal static bool Lower(byte[] image, int[] layout, int[] heaps, int method, int[] body, int[] records)
        {
            if (body[1] != 25 || body[2] < 3 || body[5] != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "Contains"))
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, method,
                    "System", "String", "Contains", new byte[] { 0x20, 1, 2, 3 }, false))
                return false;

            int start = body[0];
            if (U16(image, (uint)start) != 0x7c02 || image[start + 6] != 0x28 ||
                U32(image, (uint)(start + 11)) != 0x28026803 || image[start + 19] != 0x28 ||
                image[start + 24] != 0x2a)
                return false;

            uint field = U32(image, (uint)(start + 2));
            uint length = U32(image, (uint)(start + 15));
            int fieldRow = (int)(field & 0xffffff);
            if (field >> 24 != 4 || fieldRow < 1 || fieldRow > counts[4] ||
                !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, fieldRow) ||
                length >> 24 != 6 || !CoreStringRange.IsMethod(image, layout, heaps, counts, first,
                    (int)(length & 0xffffff), "System", "String", "get_Length", new byte[] { 0x20, 0, 8 }, false) ||
                !IsClosedMethod(image, layout, heaps, counts, first, U32(image, (uint)(start + 7)),
                    "System.Runtime.CompilerServices", "Unsafe", "As", new byte[] { 3, 6 },
                    new byte[] { 0x10, 2, 1, 0x10, 0x1e, 1, 0x10, 0x1e, 0 }) ||
                !IsClosedMethod(image, layout, heaps, counts, first, U32(image, (uint)(start + 20)),
                    "System", "SpanHelpers", "ContainsValueType", new byte[] { 6 },
                    new byte[] { 0x10, 1, 3, 2, 0x10, 0x1e, 0, 0x1e, 0, 8 }))
                return false;

            Record(records, 0, Load, 0, 11);
            Record(records, 11, Load, 1, 19);
            Record(records, 19, StringContainsCodeUnit, 0, 24);
            Record(records, 24, Return, 0, 25);
            return true;
        }

        private static bool IsClosedMethod(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            uint token, string space, string type, string name, byte[] arguments, byte[] signature)
        {
            if (token >> 24 != 0x2b) return false;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, (int)(token & 0xffffff), spec) < 0)
                return false;
            int target = spec[MethodSpecifications.Target], start = spec[MethodSpecifications.Arguments];
            if ((target & 1) != 0 || spec[MethodSpecifications.Arity] != arguments.Length ||
                spec[MethodSpecifications.End] - start != arguments.Length)
                return false;
            for (int i = 0; i < arguments.Length; i++)
                if (image[start + i] != arguments[i]) return false;
            return CoreStringRange.IsMethod(image, layout, heaps, counts, first, target >> 1, space, type, name, signature, true);
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
