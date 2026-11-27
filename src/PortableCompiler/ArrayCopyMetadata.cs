using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class ArrayCopyMetadata
    {
        private const int CoreClrOrder = 1, MonoOrder = 2;

        internal static bool ChecksIndicesBeforeRanges(BundleImages bundle)
        {
            if (bundle.ArrayCopyProfile == 0) bundle.ArrayCopyProfile = IdentifyProfile(bundle);
            return bundle.ArrayCopyProfile == MonoOrder;
        }

        private static int IdentifyProfile(BundleImages bundle)
        {
            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            var cursor = new int[1];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) continue;
                var counts = new int[45];
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) continue;
                int array = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "Array");
                if (array < 1 || ObjectLayouts.MethodRange(image, heaps, layout, array, range) < 0) continue;
                // Mono checks both negative indices before FastCopy. CoreCLR
                // checks the entire source range before the destination index.
                for (int row = range[0]; row < range[1]; row++)
                    if (IsFastCopy(image, heaps, layout, counts, first, row, cursor)) return MonoOrder;
            }
            return CoreClrOrder;
        }

        private static bool IsFastCopy(byte[] image, int[] heaps, int[] layout, int[] counts, int first, int row, int[] cursor)
        {
            if (MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x1000 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x50) != 0x10 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "FastCopy")) return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int end = SignatureReader.ReadStatic(image, heaps, blob, 5, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false; // VALUETYPE
            int handle = CompressedUnsigned(image, cursor[0], end, cursor);
            if (handle < 4 || (handle & 3) != 0 ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, handle >> 2,
                    "System.Runtime.CompilerServices", "ObjectHandleOnStack") ||
                !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11) ||
                CompressedUnsigned(image, cursor[0], end, cursor) != handle) return false;
            return SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) &&
                SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) && cursor[0] == end;
        }

        internal static int Arity(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            int signature, parent;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], "Copy")) return 0;
                parent = reference[AssemblyReferences.ParentType];
                signature = reference[5];
            }
            else
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Copy")) return 0;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1) return 0;
                parent = owner << 2;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            if (!IsArray(image, layout, heaps, parent)) return 0;
            if (Matches(image, layout, heaps, signature, 3)) return 3;
            return Matches(image, layout, heaps, signature, 5) ? 5 : 0;
        }

        private static bool Matches(byte[] image, int[] layout, int[] heaps, int signature, int arity)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, signature, arity, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void)) return false;
            for (int operand = 0; operand < arity; operand++)
            {
                if (operand == 0 || operand == (arity == 3 ? 1 : 2))
                {
                    if (!IsArray(image, layout, heaps, SignatureReader.ReadClass(image, cursor, end))) return false;
                }
                else if (!SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32)) return false;
            }
            return cursor[0] == end;
        }

        private static bool IsArray(byte[] image, int[] layout, int[] heaps, int type) =>
            type > 0 && SpanMetadata.IsPlatformType(image, layout, heaps, type, "System", "Array");
    }
}
