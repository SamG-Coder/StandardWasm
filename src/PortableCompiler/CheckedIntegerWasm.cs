using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CheckedIntegerWasm
    {
        internal static int Emit(byte[] code, int used, int opcode, int type, int left, int right)
        {
            bool unsigned = (opcode & 1) != 0;
            int operation = opcode <= 0xd7 ? 0x6a : opcode <= 0xd9 ? 0x6c : 0x6b; // add, mul, sub
            if (operation == 0x6c)
            {
                // A wrapped product divided by a nonzero factor recovers the
                // other factor only if the mathematical product fits.
                used = Local(code, used, 0x20, right);
                code[used++] = type == I32 ? (byte)0x45 : (byte)0x50; // eqz
                code[used++] = 0x45;
                code[used++] = 0x04;
                code[used++] = 0x40; // if nonzero, void block
                if (!unsigned)
                {
                    // Intercept the one overflowing Wasm division before using it as a check.
                    used = IsMinAndMinusOne(code, used, type, left, right);
                    used = ExceptionTagsWasm.ThrowIf(code, used);
                }
                used = Operation(code, used, operation, type, left, right);
                used = Local(code, used, 0x20, right);
                code[used++] = NumericOpcode(unsigned ? 0x6e : 0x6d, type);
                used = Local(code, used, 0x20, left);
                code[used++] = NumericOpcode(0x47, type); // ne
                used = ExceptionTagsWasm.ThrowIf(code, used);
                code[used++] = 0x0b;
            }
            else if (unsigned)
            {
                // Addition carries when its wrapped result is below the left
                // operand. Subtraction borrows when the right operand is larger.
                if (operation == 0x6a)
                    used = Operation(code, used, operation, type, left, right);
                else
                    used = Local(code, used, 0x20, left);
                used = Local(code, used, 0x20, operation == 0x6a ? left : right);
                code[used++] = NumericOpcode(0x49, type); // lt_u
                used = ExceptionTagsWasm.ThrowIf(code, used);
            }
            else
            {
                // The sign bit of (a ^ result) & (b ^ result) detects signed
                // addition overflow. Subtraction uses (a ^ b) in the second term.
                used = Local(code, used, 0x20, left);
                used = Operation(code, used, operation, type, left, right);
                code[used++] = NumericOpcode(0x73, type);
                used = Local(code, used, 0x20, right);
                if (operation == 0x6a)
                    used = Operation(code, used, operation, type, left, right);
                else
                    used = Local(code, used, 0x20, left);
                code[used++] = NumericOpcode(0x73, type);
                code[used++] = NumericOpcode(0x71, type);
                code[used++] = type == I32 ? (byte)0x41 : (byte)0x42;
                code[used++] = 0; // integer zero
                code[used++] = NumericOpcode(0x48, type); // lt_s
                used = ExceptionTagsWasm.ThrowIf(code, used);
            }
            return Operation(code, used, operation, type, left, right);
        }

        private static int Operation(byte[] code, int used, int opcode, int type, int left, int right)
        {
            used = Local(code, used, 0x20, left);
            used = Local(code, used, 0x20, right);
            code[used++] = NumericOpcode(opcode, type);
            return used;
        }
    }
}
