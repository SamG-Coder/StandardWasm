using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ObjectSpanMetadata
    {
        internal static bool IsDefinition(byte[] image, int[] layout, int[] heaps, int coded)
        {
            return IsPlatformType(image, layout, heaps, coded, "System", "ReadOnlySpan`1");
        }

        internal static bool IsPlatformType(byte[] image, int[] layout, int[] heaps,
            int coded, string space, string name)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            int row = coded >> 2;
            if ((coded & 3) == 0)
                return PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row, space, name);
            if ((coded & 3) != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            return AssemblyReferences.ReadType(image, heaps, layout, row, reference) >= 0 &&
                MatchReference(image, layout, heaps, counts, reference, space, name);
        }

        internal static bool MatchReference(byte[] image, int[] layout, int[] heaps, int[] counts,
            int[] reference, string space, string name)
        {
            return reference[AssemblyReferences.Depth] == 0 &&
                MatchesAscii(image, heaps, (uint)reference[2], name) &&
                MatchesAscii(image, heaps, (uint)reference[3], space) &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts,
                    reference[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false);
        }

        // TBuffer, TElement or just TElement. The returned span must be the
        // platform generic definition, not a similarly named application type.
        internal static bool MatchesFactorySignature(byte[] image, int[] layout, int[] heaps,
            int blob, int arity, int resultArgument, int inputArgument)
        {
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (length < 13 || length > limit - start || image[start] != 0x10 ||
                image[start + 1] != arity || image[start + 2] != 2 ||
                image[start + 3] != 0x15 || image[start + 4] != 0x11) return false;

            int end = start + length;
            int span = CompressedUnsigned(image, start + 5, end, next);
            int at = next[0];
            return span > 0 && end - at == 7 && IsDefinition(image, layout, heaps, span) &&
                image[at] == 1 && image[at + 1] == 0x1e && image[at + 2] == resultArgument &&
                image[at + 3] == 0x10 && image[at + 4] == 0x1e && image[at + 5] == inputArgument &&
                image[at + 6] == 8;
        }

        internal static int Accessor(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0)
                return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0) return 0;
            uint name = (uint)reference[4];
            bool length = MatchesAscii(image, heaps, name, "get_Length");
            if (!length && !MatchesAscii(image, heaps, name, "get_Item")) return 0;
            if (ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                owner, parent, objects, context, methodArguments) != ObjectSpan) return 0;
            int blob = reference[5];
            if (blob < 1 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0];
            if (size < 3 || size > limit - at || image[at] != 0x20) return 0;
            if (length) return size == 3 && image[at + 1] == 0 && image[at + 2] == 8 ? BranchIl.ObjectSpanLength : 0;
            if (image[at + 1] != 1 || image[at + 2] != 0x1f) return 0;
            int end = at + size;
            int modifier = CompressedUnsigned(image, at + 3, end, next);
            if (modifier < 1 || end - next[0] != 4 ||
                !IsPlatformType(image, layout, heaps, modifier, "System.Runtime.InteropServices", "InAttribute") ||
                U32(image, (uint)next[0]) != 0x08001310) return 0;
            return BranchIl.ObjectSpanRead;
        }
    }
}
