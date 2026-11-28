using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: platform members whose result is fixed by the wasm32 word (decision 0027)
    // or a plain 64-bit computation.
    //   static int32 IntPtr::get_Size()                    -> 4
    //   static native uint UIntPtr::get_MaxValue()          -> 0xffffffff
    //   static uint64 Math::BigMul(uint64, uint64, out uint64) -> high word, low word stored
    internal static class NativeWordMembers
    {
        internal const int PointerSize = 1, NativeUnsignedMax = 2, BigMul = 3;

        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0) return 0;
            int blob = reference[5], parent = reference[AssemblyReferences.ParentType];
            uint name = (uint)reference[4];
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            // Length 3: default static, no parameters, then the return type.
            if (U32(image, signature) == 0x08000003 && MatchesAscii(image, heaps, name, "get_Size") &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "IntPtr")) return PointerSize;
            if (U32(image, signature) == 0x19000003 && MatchesAscii(image, heaps, name, "get_MaxValue") &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "UIntPtr")) return NativeUnsignedMax;
            // Length 7: static, three parameters, uint64 (uint64, uint64, uint64&)
            if (Fits((uint)blob, 7, (uint)heaps[1]) && U32(image, signature) == 0x0b030007 &&
                U16(image, signature + 4) == 0x0b0b && image[signature + 6] == 0x10 &&
                Fits((uint)blob, 8, (uint)heaps[1]) && image[signature + 7] == 0x0b &&
                MatchesAscii(image, heaps, name, "BigMul") &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "Math")) return BigMul;
            return 0;
        }
    }
}
