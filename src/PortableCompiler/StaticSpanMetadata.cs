using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.SignatureReader;

namespace PortableCompiler
{
    internal static class StaticSpanMetadata
    {
        // Recognize the compiler-facing platform intrinsic rather than a
        // user method that returns a similarly shaped view.
        internal static int CreateSpanElement(byte[] image, int[] layout, int[] heaps, int[] counts, int row, int[] spec)
        {
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0 ||
                spec[MethodSpecifications.Arity] != 1 || spec[MethodSpecifications.End] - spec[MethodSpecifications.Arguments] != 1)
                return 0;
            int element = image[spec[MethodSpecifications.Arguments]];
            if (element < 2 || element > 0x0d) return 0;
            int target = spec[MethodSpecifications.Target], method = target >> 1;
            int name, signature;
            if ((target & 1) == 0)
            {
                int owner = ObjectLayouts.Owner(image, heaps, layout, method, false);
                if (owner < 1 || (MethodDefinitions.ReadColumn(image, layout, method, 2) & 0x2450) != 0x10 ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2, "System.Runtime.CompilerServices", "RuntimeHelpers"))
                    return 0;
                name = MethodDefinitions.ReadColumn(image, layout, method, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, method, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, method, member) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member, "System.Runtime.CompilerServices", "RuntimeHelpers"))
                    return 0;
                name = member[4];
                signature = member[5];
            }
            if (!MatchesAscii(image, heaps, (uint)name, "CreateSpan") || signature < 1 || signature >= heaps[1]) return 0;

            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + signature, limit, cursor);
            if (length < 10 || length > limit - cursor[0]) return 0;
            int end = cursor[0] + length;
            // static generic(1) ReadOnlySpan<!!0> (RuntimeFieldHandle)
            if (!TakeElement(image, cursor, end, 0x10) || !TakeElement(image, cursor, end, 1) ||
                !TakeElement(image, cursor, end, 1) || !TakeElement(image, cursor, end, 0x15) ||
                !TakeElement(image, cursor, end, 0x11)) return 0;
            int span = CompressedUnsigned(image, cursor[0], end, cursor);
            if (span <= 0 || !SpanMetadata.IsDefinition(image, layout, heaps, span) ||
                !TakeElement(image, cursor, end, 1) || !TakeElement(image, cursor, end, 0x1e) ||
                !TakeElement(image, cursor, end, 0) || !TakeElement(image, cursor, end, 0x11)) return 0;
            int field = CompressedUnsigned(image, cursor[0], end, cursor);
            return field > 0 && cursor[0] == end &&
                SpanMetadata.IsPlatformType(image, layout, heaps, field, "System", "RuntimeFieldHandle") ? element : 0;
        }
    }
}
