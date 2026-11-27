using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class ArrayClearMetadata
    {
        internal static int Arity(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            int signature, parent;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], "Clear")) return 0;
                parent = reference[AssemblyReferences.ParentType];
                signature = reference[5];
            }
            else
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Clear")) return 0;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1) return 0;
                parent = owner << 2;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            if (!IsArray(image, layout, heaps, parent)) return 0;
            if (Matches(image, layout, heaps, signature, 1)) return 1;
            return Matches(image, layout, heaps, signature, 3) ? 3 : 0;
        }

        private static bool Matches(byte[] image, int[] layout, int[] heaps, int signature, int arity)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, signature, arity, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) ||
                !IsArray(image, layout, heaps, SignatureReader.ReadClass(image, cursor, end))) return false;
            if (arity == 3 && (!SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) ||
                !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32))) return false;
            return cursor[0] == end;
        }

        private static bool IsArray(byte[] image, int[] layout, int[] heaps, int type) =>
            type > 0 && SpanMetadata.IsPlatformType(image, layout, heaps, type, "System", "Array");
    }
}
