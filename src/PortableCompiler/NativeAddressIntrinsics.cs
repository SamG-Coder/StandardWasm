using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class NativeAddressIntrinsics
    {
        internal static int Identify(byte[] image, int[] layout, int[] heaps, int[] counts, int[] spec)
        {
            if (spec[MethodSpecifications.Arity] != 1) return 0;
            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int signature, name;
            if ((target & 1) == 0)
            {
                int type = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (type < 1 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, type << 2,
                        "System.Runtime.CompilerServices", "Unsafe")) return 0;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                        "System.Runtime.CompilerServices", "Unsafe")) return 0;
                name = member[4];
                signature = member[5];
            }
            if (signature < 1 || !Fits((uint)signature, 7, (uint)heaps[1])) return 0;
            uint at = (uint)(heaps[0] + signature);
            // Each signature includes its blob length and the generic calling convention.
            uint header = U32(image, at);
            if (header == 0x00011006 && U16(image, at + 4) == 0x1e10 && image[at + 6] == 0 &&
                MatchesAscii(image, heaps, (uint)name, "NullRef")) return NullManagedLocation;
            if (header == 0x01011007 && Fits((uint)signature, 8, (uint)heaps[1]) &&
                U32(image, at + 4) == 0x001e1002 && MatchesAscii(image, heaps, (uint)name, "IsNullRef"))
                return TestNullLocation;
            if (header == 0x01011008 && Fits((uint)signature, 9, (uint)heaps[1]) &&
                U32(image, at + 4) == 0x1e10010f && image[at + 8] == 0 &&
                MatchesAscii(image, heaps, (uint)name, "AsPointer")) return NativeAddressView;
            return 0;
        }
    }
}
