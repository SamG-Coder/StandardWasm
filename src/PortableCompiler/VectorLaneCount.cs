using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class VectorLaneCount
    {
        // Count is 16 / sizeof(T) after the upstream exact lane-type guard.
        // No SIMD capability is implied by knowing the closed layout width.
        internal static int Identify(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                reference[AssemblyReferences.GenericParent] == 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "get_Count"))
                return 0;
            int signature = reference[5];
            if (signature <= 0 || signature > heaps[1] - 4 ||
                U32(image, (uint)(heaps[0] + signature)) != 0x08000003)
                return 0;

            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + reference[AssemblyReferences.GenericParent], limit, cursor);
            int start = cursor[0];
            if (size < 5 || size > limit - start || image[start] != 0x15) return 0;
            int end = start + size;
            cursor[0] = start + 1;
            var head = new int[2];
            if (SignatureReader.ReadGenericHeader(image, cursor, end, head) != 1 || head[1] != 0x11 ||
                !IsVector128(image, layout, heaps, head[0])) return 0;
            int lane = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner, cursor, end,
                context, objects, 0, methodArguments: methodArguments);
            if (cursor[0] != end || !GenericScalar.IsScalar(lane)) return 0;
            switch (GenericScalar.Element(lane))
            {
                case 4: case 5: return 16;
                case 6: case 7: return 8;
                case 8: case 9: case 0x0c: case 0x18: case 0x19: return 4;
                case 0x0a: case 0x0b: case 0x0d: return 2;
                default: return 0;
            }
        }

        internal static bool IsVector128(byte[] image, int[] layout, int[] heaps, int coded)
        {
            const string space = "System.Runtime.Intrinsics";
            const string name = "Vector128`1";
            if ((coded & 3) == 0) return SpanMetadata.IsPlatformType(image, layout, heaps, coded, space, name);
            if ((coded & 3) != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadType(image, heaps, layout, coded >> 2, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[2], name) ||
                !MatchesAscii(image, heaps, (uint)reference[3], space)) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsIntrinsicsAssembly(image, heaps, layout, counts,
                    reference[AssemblyReferences.AssemblyRow]);
        }
    }
}
