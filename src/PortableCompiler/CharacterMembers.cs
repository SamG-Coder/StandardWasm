using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: platform System.Char members whose CLR result is a fixed function of the
    // UTF-16 code unit, lowered to Wasm comparisons instead of CoreLib's Latin-1 and Unicode tables.
    internal static class CharacterMembers
    {
        internal const int IsWhiteSpace = 1, Capacity = 128;

        // static bool System.Char::IsWhiteSpace(char) on an authentic platform TypeRef.
        internal static int Identify(byte[] image, int[] heaps, int[] layout, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "IsWhiteSpace")) return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 5, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            // Length 4: default static, one parameter, returns Boolean, takes Char.
            if (U32(image, signature) != 0x02010004 || image[signature + 4] != 0x03) return 0;
            // PrimitiveElement also proves that the TypeRef resolves to a platform assembly.
            return ArrayTypeRefs.PrimitiveElement(image, heaps, layout, reference[AssemblyReferences.Leaf] << 2 | 1) == 3
                ? IsWhiteSpace : 0;
        }

        // [char as i32] -> [i32]: the UnicodeCategory SpaceSeparator, LineSeparator and
        // ParagraphSeparator code points plus U+0009..U+000D and U+0085, exactly as
        // Char.IsWhiteSpace on .NET 10 (Unicode 15.1). `value` holds the zero-extended code unit.
        internal static int EmitIsWhiteSpace(byte[] code, int used, int value)
        {
            used = InRange(code, used, value, 0x09, 0x0d);
            used = OrEqual(code, used, value, 0x20);
            used = OrEqual(code, used, value, 0x85);
            used = OrEqual(code, used, value, 0xa0);
            used = OrEqual(code, used, value, 0x1680);
            used = OrEqual(code, used, value, 0x2028);
            used = OrEqual(code, used, value, 0x2029);
            used = OrEqual(code, used, value, 0x202f);
            used = OrEqual(code, used, value, 0x205f);
            used = OrEqual(code, used, value, 0x3000);
            used = InRange(code, used, value, 0x2000, 0x200a);
            code[used++] = 0x72;
            return used;
        }

        // [flag] -> [flag | value == point]
        private static int OrEqual(byte[] code, int used, int value, int point)
        {
            used = BranchWasm.Local(code, used, 0x20, value);
            used = BranchWasm.Const(code, used, point);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x72; // i32.or
            return used;
        }

        // (value - first) <=u (last - first)
        private static int InRange(byte[] code, int used, int value, int first, int last)
        {
            used = BranchWasm.Local(code, used, 0x20, value);
            used = BranchWasm.Const(code, used, first);
            code[used++] = 0x6b; // i32.sub
            used = BranchWasm.Const(code, used, last - first);
            code[used++] = 0x4d; // i32.le_u
            return used;
        }
    }
}
