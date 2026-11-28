using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0104: System.Numerics.BitOperations.LeadingZeroCount, TrailingZeroCount and PopCount.
    // Each is exactly one Wasm instruction: clz, ctz and popcnt return the operand width for zero,
    // as .NET does. The 64-bit forms wrap their i64 count to the int32 result. Native-int overloads
    // use the 32-bit instruction on wasm32 (decision 0027).
    internal static class BitOperationMembers
    {
        internal const int LeadingZeroCount = 1, TrailingZeroCount = 2, PopCount = 3;
        // The operand packs the operation and whether the argument is 64-bit (Wide) or native (Native).
        internal const int Wide = 16, NativeWord = 32;

        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType],
                    "System.Numerics", "BitOperations")) return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 5, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            // Length 4: default static, one parameter, returns int32, then the argument type.
            if (U32(image, signature) != 0x08010004) return 0;
            int argument = image[signature + 4];
            uint name = (uint)reference[4];
            int operation = MatchesAscii(image, heaps, name, "LeadingZeroCount") ? LeadingZeroCount :
                MatchesAscii(image, heaps, name, "TrailingZeroCount") ? TrailingZeroCount :
                MatchesAscii(image, heaps, name, "PopCount") ? PopCount : 0;
            if (operation == 0) return 0;
            // LeadingZeroCount and PopCount take unsigned arguments; TrailingZeroCount also has signed ones.
            bool signedAllowed = operation == TrailingZeroCount;
            if (argument == 0x09 || signedAllowed && argument == 0x08) return operation;
            if (argument == 0x0b || signedAllowed && argument == 0x0a) return operation | Wide;
            if (argument == 0x19 || signedAllowed && argument == 0x18) return operation | NativeWord;
            return 0;
        }

        internal static int ArgumentType(int operand) =>
            (operand & Wide) != 0 ? I64 : (operand & NativeWord) != 0 ? Native : I32;

        // [value] -> [int32 count]
        internal static int Emit(byte[] code, int used, int operand)
        {
            int operation = operand & 15;
            bool wide = (operand & Wide) != 0;
            byte instruction = operation == LeadingZeroCount ? (byte)0x67 : operation == TrailingZeroCount ? (byte)0x68 : (byte)0x69;
            code[used++] = wide ? (byte)(instruction + 0x12) : instruction; // i64.clz/ctz/popcnt are 0x79..0x7b
            if (wide) code[used++] = 0xa7; // i32.wrap_i64
            return used;
        }
    }
}
