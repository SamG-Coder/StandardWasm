using static PeLibrary.PlatformMembers;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Wasm sequences for the platform System.Math intrinsics recognized by PlatformMembers (decision 0003). Each one
    // is bit-identical to .NET 10 for every input, with NaN payloads aside:
    //   - Sqrt, Floor, Ceiling, Truncate, Round(ToEven) and floating Abs are single IEEE instructions.
    //   - Floating Max/Min propagate NaN and order -0 below +0, as .NET Core 3.0+ does.
    //   - Integer Max/Min select with the parameter's signedness.
    //   - Integer Abs traps on MinValue, where .NET throws OverflowException.
    internal static class MathWasm
    {
        internal static byte Type(int intrinsic)
        {
            if (intrinsic == AbsF32 || intrinsic == MaxF32 || intrinsic == MinF32) return F32;
            if (intrinsic >= MaxI32 && intrinsic <= MinU32 || intrinsic == AbsI32) return I32;
            if (intrinsic >= MaxI64 && intrinsic <= MinU64 || intrinsic == AbsI64) return I64;
            return F64;
        }

        internal static int Arity(int intrinsic) => intrinsic >= MaxF64 && intrinsic <= MinU64 ? 2 : 1;

        // left and right are the spill locals holding the operands. A unary intrinsic ignores right.
        internal static int Emit(byte[] code, int used, int intrinsic, int left, int right)
        {
            if (intrinsic == AbsI32 || intrinsic == AbsI64)
            {
                bool wide = intrinsic == AbsI64;
                used = BranchWasm.Local(code, used, 0x20, left);
                used = wide ? Wide(code, used, 0, int.MinValue) : BranchWasm.Const(code, used, int.MinValue);
                code[used++] = wide ? (byte)0x51 : (byte)0x46; // eq
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                code[used++] = 0x00; // unreachable
                code[used++] = 0x0b;
                used = wide ? Wide(code, used, 0, 0) : BranchWasm.Const(code, used, 0);
                used = BranchWasm.Local(code, used, 0x20, left);
                code[used++] = wide ? (byte)0x7d : (byte)0x6b; // sub: 0 - x
                used = BranchWasm.Local(code, used, 0x20, left);
                used = BranchWasm.Local(code, used, 0x20, left);
                used = wide ? Wide(code, used, 0, 0) : BranchWasm.Const(code, used, 0);
                code[used++] = wide ? (byte)0x53 : (byte)0x48; // lt_s
                code[used++] = 0x1b; // select
                return used;
            }
            if (intrinsic >= MaxI32 && intrinsic <= MinU64)
            {
                used = BranchWasm.Local(code, used, 0x20, left);
                used = BranchWasm.Local(code, used, 0x20, right);
                used = BranchWasm.Local(code, used, 0x20, left);
                used = BranchWasm.Local(code, used, 0x20, right);
                code[used++] = (byte)Selector(intrinsic);
                code[used++] = 0x1b; // select left when the comparison holds
                return used;
            }
            used = BranchWasm.Local(code, used, 0x20, left);
            if (Arity(intrinsic) == 2) used = BranchWasm.Local(code, used, 0x20, right);
            code[used++] = (byte)FloatOpcode(intrinsic);
            return used;
        }

        private static int Selector(int intrinsic)
        {
            if (intrinsic == MaxI32) return 0x4a; // i32.gt_s
            if (intrinsic == MinI32) return 0x48; // i32.lt_s
            if (intrinsic == MaxU32) return 0x4b; // i32.gt_u
            if (intrinsic == MinU32) return 0x49; // i32.lt_u
            if (intrinsic == MaxI64) return 0x55; // i64.gt_s
            if (intrinsic == MinI64) return 0x53; // i64.lt_s
            if (intrinsic == MaxU64) return 0x56; // i64.gt_u
            return 0x54; // i64.lt_u
        }

        private static int FloatOpcode(int intrinsic)
        {
            if (intrinsic == SqrtF64) return 0x9f;
            if (intrinsic == AbsF64) return 0x99;
            if (intrinsic == CeilingF64) return 0x9b;
            if (intrinsic == FloorF64) return 0x9c;
            if (intrinsic == TruncateF64) return 0x9d;
            if (intrinsic == RoundF64) return 0x9e; // f64.nearest: ties to even
            if (intrinsic == MinF64) return 0xa4;
            if (intrinsic == MaxF64) return 0xa5;
            if (intrinsic == AbsF32) return 0x8b;
            if (intrinsic == MinF32) return 0x96;
            return 0x97; // f32.max
        }

        private static int Wide(byte[] code, int used, int low, int high)
        {
            code[used++] = 0x42; // i64.const
            return WasmBinary.Signed64(code, used, low, high);
        }
    }
}
