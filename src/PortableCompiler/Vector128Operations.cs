using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.SignatureReader;

namespace PortableCompiler
{
    internal static class Vector128Operations
    {
        internal const int Broadcast = 1, ExtractLane = 2;

        internal static bool IsWordBroadcast(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            int blob = StaticMethod(image, layout, heaps, row, member, "Create");
            var cursor = new int[1];
            int end = ReadStatic(image, heaps, blob, 1, cursor);
            return end >= 0 && IsVector(image, layout, heaps, cursor, end, 7) &&
                TakeElement(image, cursor, end, 7) && cursor[0] == end;
        }

        internal static bool IsWordExtraction(byte[] image, int[] layout, int[] heaps,
            int[] spec, int[] methodArguments)
        {
            int offset = spec[MethodSpecifications.Arguments], stop = spec[MethodSpecifications.End];
            if (spec[MethodSpecifications.Arity] != 1 || offset >= stop) return false;
            var cursor = new int[1];
            if (image[offset] == 0x1e)
            {
                int arg = CompressedUnsigned(image, offset + 1, stop, cursor);
                if (methodArguments == null || arg < 0 || arg >= methodArguments.Length || cursor[0] != stop ||
                    methodArguments[arg] != GenericScalar.Key(7)) return false;
            }
            else if (image[offset] != 7 || offset + 1 != stop) return false;

            int target = spec[MethodSpecifications.Target];
            int blob = StaticMethod(image, layout, heaps, target >> 1, (target & 1) != 0, "GetElement");
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int at = cursor[0];
            if (size <= 0 || size > limit - at) return false;
            int end = at + size;
            return TakeElement(image, cursor, end, 0x10) && TakeElement(image, cursor, end, 1) &&
                TakeElement(image, cursor, end, 2) && TakeElement(image, cursor, end, 0x1e) &&
                TakeElement(image, cursor, end, 0) && IsVector(image, layout, heaps, cursor, end, 0x1e) &&
                TakeElement(image, cursor, end, 0) && TakeElement(image, cursor, end, 8) && cursor[0] == end;
        }

        private static bool IsVector(byte[] image, int[] layout, int[] heaps, int[] cursor, int end, int element)
        {
            int coded = ReadSingleArgumentValue(image, cursor, end);
            return coded > 0 && VectorLaneCount.IsVector128(image, layout, heaps, coded) &&
                TakeElement(image, cursor, end, element);
        }

        private static int StaticMethod(byte[] image, int[] layout, int[] heaps, int row, bool member, string name)
        {
            const string space = "System.Runtime.Intrinsics";
            var counts = new int[45];
            if (!member)
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name) ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2057) != 0x16) return 0;
                int start = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (start < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                return ObjectLayouts.NamedDefinition(image, heaps, layout, counts, start, owner, space, "Vector128")
                    ? MethodDefinitions.ReadColumn(image, layout, row, 4) : 0;
            }

            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], name) ||
                !MatchesAscii(image, heaps, (uint)reference[3], space) ||
                !MatchesAscii(image, heaps, (uint)reference[2], "Vector128") ||
                MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            return PlatformAssemblyIdentity.IsIntrinsicsAssembly(image, heaps, layout, counts,
                reference[AssemblyReferences.AssemblyRow]) ? reference[5] : 0;
        }
    }
}
