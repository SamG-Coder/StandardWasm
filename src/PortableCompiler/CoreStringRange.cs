using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class CoreStringRange
    {
        // The CoreLib helper allocates a fresh string and copies its UTF-16
        // units before the result escapes. Keep the public caller's checks
        // and replace only this verified storage sequence.
        internal static bool Lower(byte[] image, int[] layout, int[] heaps, int method, int[] body, int[] records)
        {
            if (body[1] != 36 || body[2] < 4 || body[5] != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "InternalSubString"))
                return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !IsMethod(image, layout, heaps, counts, first, method, "System", "String",
                    "InternalSubString", new byte[] { 0x20, 2, 0x0e, 8, 8 }, false))
                return false;

            int start = body[0];
            // ldarg.2; conv.i; call allocate; ldarg.2; conv.u; stloc.0;
            // dup; ldflda; ldarg.0; ldflda; ldarg.1; conv.u; call Add;
            // ldloc.0; call Memmove; ret. No interior entry or handler.
            if (U16(image, (uint)start) != 0xd304 || image[start + 2] != 0x28 ||
                U32(image, (uint)(start + 7)) != 0x250ae004 || image[start + 11] != 0x7c ||
                U16(image, (uint)(start + 16)) != 0x7c02 || U16(image, (uint)(start + 22)) != 0xe003 ||
                image[start + 24] != 0x28 || U16(image, (uint)(start + 29)) != 0x2806 ||
                image[start + 35] != 0x2a) return false;

            uint field = U32(image, (uint)(start + 12));
            int fieldRow = (int)(field & 0xffffff);
            if (field >> 24 != 4 || fieldRow < 1 || fieldRow > counts[4] ||
                U32(image, (uint)(start + 18)) != field ||
                !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, fieldRow)) return false;

            uint allocate = U32(image, (uint)(start + 3));
            if (allocate >> 24 != 6 ||
                !IsMethod(image, layout, heaps, counts, first, (int)(allocate & 0xffffff),
                    "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, 0x18 }, true) ||
                !IsCharacterMethod(image, layout, heaps, counts, first, U32(image, (uint)(start + 25)),
                    "System.Runtime.CompilerServices", "Unsafe", "Add",
                    new byte[] { 0x10, 1, 2, 0x10, 0x1e, 0, 0x10, 0x1e, 0, 0x18 }) ||
                !IsCharacterMemmove(image, layout, heaps, counts, first, U32(image, (uint)(start + 31)))) return false;

            Record(records, 0, BranchIl.Load, 0, 1);
            Record(records, 1, BranchIl.Load, 1, 2);
            Record(records, 2, BranchIl.Load, 2, 7);
            Record(records, 7, BranchIl.StringRange, 0, 35);
            Record(records, 35, BranchIl.Return, 0, 36);
            return true;
        }

        private static void Record(int[] records, int offset, int kind, int operand, int next)
        {
            int slot = offset * BranchIl.Width;
            records[slot] = kind;
            records[slot + 1] = operand;
            records[slot + 2] = next;
        }

        internal static bool IsCharacterMemmove(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, uint token) =>
            IsCharacterMethod(image, layout, heaps, counts, first, token, "System", "Buffer", "Memmove",
                new byte[] { 0x10, 1, 3, 1, 0x10, 0x1e, 0, 0x10, 0x1e, 0, 0x19 });

        private static bool IsCharacterMethod(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            uint token, string space, string type, string name, byte[] signature) =>
            IsClosedMethod(image, layout, heaps, counts, first, token, space, type, name, new byte[] { 3 }, signature);

        internal static bool IsClosedMethod(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
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
            return IsMethod(image, layout, heaps, counts, first, target >> 1, space, type, name, signature, true);
        }

        internal static bool IsMethod(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int row, string space, string type, string name, byte[] signature, bool isStatic)
        {
            if (row < 1 || row > counts[6] ||
                ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) != 0) != isStatic ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name))
                return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, space, type))
                return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int end = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, end, next);
            int start = next[0];
            if (size != signature.Length || size > end - start) return false;
            for (int i = 0; i < size; i++)
                if (image[start + i] != signature[i]) return false;
            return true;
        }
    }
}
