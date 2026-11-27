using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CharacterSpanCallbackMetadata
    {
        internal const int Construct = 1, Invoke = 2;

        internal static int Member(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0)
                return 0;
            uint name = (uint)reference[4];
            bool construct = MatchesAscii(image, heaps, name, ".ctor");
            if (!construct && !MatchesAscii(image, heaps, name, "Invoke")) return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (!IsPlatformParent(image, layout, heaps, parent) ||
                ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                    owner, parent, objects, context, methodArguments) != CharacterSpanCallback) return 0;

            int blob = reference[5];
            if (blob <= 0 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 5 || size > limit - start || image[start] != 0x20 ||
                image[start + 1] != 2 || image[start + 2] != 1) return 0;
            if (construct) return size == 5 && image[start + 3] == 0x1c && image[start + 4] == 0x18 ? Construct : 0;
            int end = start + size;
            int argument = SpanMetadata.OpenSpan(image, layout, heaps, start + 3, end, next, true);
            return argument >= 0 && end - argument == 2 &&
                image[argument] == 0x13 && image[argument + 1] == 1 ? Invoke : 0;
        }

        private static bool IsPlatformParent(byte[] image, int[] layout, int[] heaps, int blob)
        {
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 5 || size > limit - start || image[start] != 0x15 || image[start + 1] != 0x12)
                return false;
            int coded = CompressedUnsigned(image, start + 2, start + size, next);
            return coded > 0 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System.Buffers", "SpanAction`2");
        }

        internal static bool MatchesTarget(int[] types, int shape, int arity, bool instance)
        {
            int first = shape + (instance ? 2 : 1);
            return arity == (instance ? 3 : 2) && types[shape] == Void &&
                types[first] == WritableCharacterSpan && types[first + 1] == Native;
        }
    }
}
