using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // Decision 0104: `(ulong)((UInt128)a * b / c)` with every UInt128 value an evaluation-stack
    // temporary, the shape C# emits for an exact tick-to-nanosecond conversion:
    //
    //   [a] call UInt128::op_Implicit(uint64)
    //   [b] call UInt128::op_Implicit(uint64)        (b and c are each pushed by one record)
    //       call UInt128::op_Multiply(UInt128, UInt128)
    //   [c] call UInt128::op_Implicit(uint64)
    //       call UInt128::op_Division(UInt128, UInt128)
    //       call uint64 UInt128::op_Explicit(UInt128)
    //
    // The sequence becomes [a, b, c] -> [uint64]: the full 128-bit product, a 128-by-64 bit long
    // division and the low 64 quotient bits, which is the truncation op_Explicit performs. A zero
    // divisor throws DivideByZeroException, as UInt128 division does. Other UInt128 uses still
    // need the type's own layout and stay diagnosed.
    internal static class WideMultiplyDivide
    {
        internal const int Capacity = 360;
        private const int Implicit = 1, Multiply = 2, Division = 3, Explicit = 4;

        // Rewrites every fusable sequence in the decoded records of one method body.
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            for (int first = 0; first < records.Length; first += Width)
            {
                if (Member(image, layout, heaps, records, first) != Implicit) continue;
                int b = Operand(records, Next(records, first, clauses), clauses);
                int widenB = Next(records, b, clauses);
                int multiply = Next(records, widenB, clauses);
                int c = Operand(records, Next(records, multiply, clauses), clauses);
                int widenC = Next(records, c, clauses);
                int divide = Next(records, widenC, clauses);
                int narrow = Next(records, divide, clauses);
                if (narrow < 0 ||
                    Member(image, layout, heaps, records, widenB) != Implicit ||
                    Member(image, layout, heaps, records, multiply) != Multiply ||
                    Member(image, layout, heaps, records, widenC) != Implicit ||
                    Member(image, layout, heaps, records, divide) != Division ||
                    Member(image, layout, heaps, records, narrow) != Explicit) continue;
                records[first] = Nop;
                records[widenB] = Nop;
                records[multiply] = Nop;
                records[widenC] = Nop;
                records[divide] = Nop;
                records[narrow] = MultiplyDivideUInt64;
                records[narrow + 1] = 0;
                records[narrow + 3] = 0;
            }
        }

        // The record that follows `slot` in straight-line order, or -1 when `slot` is invalid
        // or the follower is a branch target (its stack would not be this sequence's alone).
        private static int Next(int[] records, int slot, int[] clauses)
        {
            if (slot < 0) return -1;
            int next = records[slot + 2] * Width;
            return next < records.Length && !ValueOperands.Entry(records, next / Width, clauses) ? next : -1;
        }

        // The last record of an operand that pushes one 64-bit value and reads nothing else:
        // ldloc/ldarg, ldc.i8 or ldsfld, or one of those followed by conv.i8/conv.u8
        // (ldc.i4 then conv.i8 is how C# widens a constant). -1 for any other shape.
        private static int Operand(int[] records, int slot, int[] clauses)
        {
            if (slot < 0) return -1;
            int kind = records[slot];
            if (kind != Load && kind != WideConstant && kind != LoadStaticField && kind != Constant) return -1;
            int next = Next(records, slot, clauses);
            if (next >= 0 && records[next] == ConvertInteger && (records[next + 1] == 0x6a || records[next + 1] == 0x6e))
                return next;
            return kind == Constant ? -1 : slot;
        }

        private static int Member(byte[] image, int[] layout, int[] heaps, int[] records, int slot)
        {
            if (slot < 0 || records[slot] != MemberCall || records[slot + 3] != 0x28) return 0;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, records[slot + 1], reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType], "System", "UInt128"))
                return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 2, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob), name = (uint)reference[4];
            // static valuetype UInt128 op_Implicit(uint64): length 5 (0x11 + one-byte TypeRef index)
            // static valuetype UInt128 op_Multiply/op_Division(UInt128, UInt128): 0x11 tokens
            // static uint64 op_Explicit(valuetype UInt128)
            int length = image[signature];
            if (length < 4 || !Fits((uint)blob, (uint)(length + 1), (uint)heaps[1]) || image[signature + 1] != 0) return 0;
            int count = image[signature + 2];
            if (MatchesAscii(image, heaps, name, "op_Implicit"))
                return count == 1 && image[signature + 3] == 0x11 && image[signature + length] == 0x0b ? Implicit : 0;
            if (MatchesAscii(image, heaps, name, "op_Explicit"))
                return count == 1 && image[signature + 3] == 0x0b && image[signature + 4] == 0x11 ? Explicit : 0;
            if (count != 2 || image[signature + 3] != 0x11) return 0;
            if (MatchesAscii(image, heaps, name, "op_Multiply")) return Multiply;
            return MatchesAscii(image, heaps, name, "op_Division") ? Division : 0;
        }

        // [a, b, c] in i64 locals -> quotient in `a`. `hi` and `remainder` are i64 scratch,
        // `bit` an i32 scratch local. The product is hi:lo with lo written into `b`.
        internal static int Emit(byte[] code, int used, int a, int b, int c, int hi, int remainder, int bit)
        {
            used = Get(code, used, c);
            code[used++] = 0x50; // i64.eqz
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.DivideTag);

            used = High(code, used, a, b);
            used = Set(code, used, hi);
            used = Get(code, used, a);
            used = Get(code, used, b);
            code[used++] = 0x7e; // i64.mul: the low product word
            used = Set(code, used, b);
            return Divide(code, used, a, b, c, hi, remainder, bit);
        }

        // [] -> [the high 64 bits of the unsigned 128-bit product a * b]:
        // a1*b1 + (a0*b1 >> 32) + (a1*b0 >> 32) + ((a0*b0 >> 32) + low32(a0*b1) + low32(a1*b0)) >> 32
        internal static int High(byte[] code, int used, int a, int b)
        {
            used = Product(code, used, a, true, b, true);
            used = Product(code, used, a, false, b, true);
            used = Shift(code, used);
            code[used++] = 0x7c; // i64.add
            used = Product(code, used, a, true, b, false);
            used = Shift(code, used);
            code[used++] = 0x7c;
            used = Product(code, used, a, false, b, false);
            used = Shift(code, used);
            used = Product(code, used, a, false, b, true);
            used = Low(code, used);
            code[used++] = 0x7c;
            used = Product(code, used, a, true, b, false);
            used = Low(code, used);
            code[used++] = 0x7c;
            used = Shift(code, used);
            code[used++] = 0x7c;
            return used;
        }

        // Restoring long division of hi:lo (lo in `b`) by c; the low 64 quotient bits go to `a`.
        private static int Divide(byte[] code, int used, int a, int b, int c, int hi, int remainder, int bit)
        {
            // Restoring long division over the 128 dividend bits, most significant first.
            used = WasmConst64(code, used, 0);
            used = Set(code, used, remainder);
            used = WasmConst64(code, used, 0);
            used = Set(code, used, a); // quotient
            used = BranchWasm.Const(code, used, 127);
            used = BranchWasm.Local(code, used, 0x21, bit);
            code[used++] = 0x03; code[used++] = 0x40; // loop
            // carry = remainder >> 63; remainder = remainder << 1 | dividend bit
            used = Get(code, used, remainder);
            code[used++] = 0x42; code[used++] = 63; // i64.const 63
            code[used++] = 0x88; // i64.shr_u
            code[used++] = 0xa7; // i32.wrap_i64: carry on the stack for later
            used = Get(code, used, remainder);
            code[used++] = 0x42; code[used++] = 1;
            code[used++] = 0x86; // i64.shl
            used = Get(code, used, hi);
            used = Get(code, used, b);
            used = BranchWasm.Local(code, used, 0x20, bit);
            used = BranchWasm.Const(code, used, 64);
            code[used++] = 0x4e; // i32.ge_s: bit >= 64 selects the high word
            code[used++] = 0x1b; // select
            used = BranchWasm.Local(code, used, 0x20, bit);
            code[used++] = 0xad; // i64.extend_i32_u (shift counts are taken mod 64)
            code[used++] = 0x88; // i64.shr_u
            code[used++] = 0x42; code[used++] = 1;
            code[used++] = 0x83; // i64.and
            code[used++] = 0x84; // i64.or
            used = Set(code, used, remainder);
            // [carry] -> carry | remainder >= c
            used = Get(code, used, remainder);
            used = Get(code, used, c);
            code[used++] = 0x5a; // i64.ge_u
            code[used++] = 0x72; // i32.or
            code[used++] = 0x04; code[used++] = 0x40; // if
            used = Get(code, used, remainder);
            used = Get(code, used, c);
            code[used++] = 0x7d; // i64.sub
            used = Set(code, used, remainder);
            used = BranchWasm.Local(code, used, 0x20, bit);
            used = BranchWasm.Const(code, used, 64);
            code[used++] = 0x48; // i32.lt_s: only the low 64 quotient bits are kept
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, a);
            code[used++] = 0x42; code[used++] = 1;
            used = BranchWasm.Local(code, used, 0x20, bit);
            code[used++] = 0xad;
            code[used++] = 0x86; // i64.shl
            code[used++] = 0x84; // i64.or
            used = Set(code, used, a);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            // while (bit-- > 0)
            used = BranchWasm.Local(code, used, 0x20, bit);
            used = BranchWasm.Local(code, used, 0x20, bit);
            used = BranchWasm.Const(code, used, 1);
            code[used++] = 0x6b; // i32.sub
            used = BranchWasm.Local(code, used, 0x21, bit);
            code[used++] = 0x0d; code[used++] = 0; // br_if loop while the old bit was nonzero
            code[used++] = 0x0b; // end loop
            return used;
        }

        // [] -> [x_half * y_half] for the selected 32-bit halves (high when `high`).
        private static int Product(byte[] code, int used, int x, bool xHigh, int y, bool yHigh)
        {
            used = Half(code, used, x, xHigh);
            used = Half(code, used, y, yHigh);
            code[used++] = 0x7e; // i64.mul
            return used;
        }

        private static int Half(byte[] code, int used, int local, bool high)
        {
            used = Get(code, used, local);
            if (high)
            {
                code[used++] = 0x42; code[used++] = 32;
                code[used++] = 0x88; // i64.shr_u
                return used;
            }
            return Low(code, used);
        }

        private static int Low(byte[] code, int used)
        {
            code[used++] = 0x42; // i64.const 0xffffffff
            code[used++] = 0xff; code[used++] = 0xff; code[used++] = 0xff; code[used++] = 0xff; code[used++] = 0x0f;
            code[used++] = 0x83; // i64.and
            return used;
        }

        private static int Shift(byte[] code, int used)
        {
            code[used++] = 0x42; code[used++] = 32;
            code[used++] = 0x88;
            return used;
        }

        private static int WasmConst64(byte[] code, int used, int value)
        {
            code[used++] = 0x42;
            code[used++] = (byte)value;
            return used;
        }

        private static int Get(byte[] code, int used, int local) => BranchWasm.Local(code, used, 0x20, local);
        private static int Set(byte[] code, int used, int local) => BranchWasm.Local(code, used, 0x21, local);
    }
}
