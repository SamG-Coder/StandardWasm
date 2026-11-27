using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class NativeAddressIntrinsics
    {
        internal static bool IsAsPointer(byte[] image, int[] layout, int[] heaps, int[] counts, int[] spec)
        {
            if (spec[MethodSpecifications.Arity] != 1) return false;
            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int signature, name;
            if ((target & 1) == 0)
            {
                int type = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (type < 1 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, type << 2,
                        "System.Runtime.CompilerServices", "Unsafe")) return false;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                        "System.Runtime.CompilerServices", "Unsafe")) return false;
                name = member[4];
                signature = member[5];
            }
            if (signature < 1 || !Fits((uint)signature, 9, (uint)heaps[1]) ||
                !MatchesAscii(image, heaps, (uint)name, "AsPointer")) return false;
            uint at = (uint)(heaps[0] + signature);
            // Length 8, then static generic void* AsPointer<T>(ref T).
            return U32(image, at) == 0x01011008 && U32(image, at + 4) == 0x1e10010f && image[at + 8] == 0;
        }
    }
}
