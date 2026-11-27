using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // A CharacterLocation backed by a GC array stores a UTF-16 element index.
    // Prove the offset's low bit is zero before expressing bytes in that ABI.
    internal static class CharacterOffsetProof
    {
        private const int Invalid = -1, Even = 0, Odd = 1, Unknown = 2;

        internal static bool IsAligned(int[] records, int slot, int[] clauses) =>
            Parity(records, new[] { slot }, clauses, 16) == Even;

        private static int Parity(int[] records, int[] next, int[] clauses, int depth)
        {
            if (depth == 0 || ValueOperands.Entry(records, next[0] / Width, clauses)) return Invalid;
            int slot = next[0] - Width;
            while (slot >= 0 && records[slot] == 0) slot -= Width;
            if (slot < 0 || records[slot + 2] * Width != next[0]) return Invalid;
            next[0] = slot;

            int kind = records[slot], operand = records[slot + 1];
            if (kind == Constant || kind == WideConstant) return operand & 1;
            if (kind == Load) return Unknown;
            if (kind == ConvertInteger || kind == Narrow)
                return Parity(records, next, clauses, depth - 1);
            if (kind != Binary) return Invalid;

            int right = Parity(records, next, clauses, depth - 1);
            int left = Parity(records, next, clauses, depth - 1);
            if (left == Invalid || right == Invalid) return Invalid;
            if (operand == 0x6c || operand == 0x71) // mul, and
                return left == Even || right == Even ? Even : left == Odd && right == Odd ? Odd : Unknown;
            if (operand == 0x6a || operand == 0x6b || operand == 0x73) // add, sub, xor
                return left == Unknown || right == Unknown ? Unknown : left ^ right;
            // An odd shift count is never zero modulo the operand width.
            if (operand == 0x74 && (left == Even || right == Odd)) return Even;
            return Unknown;
        }
    }
}
