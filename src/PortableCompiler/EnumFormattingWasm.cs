using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class EnumFormattingWasm
    {
        internal const int Capacity = 700, NameCapacity = 88;

        internal static int Emit(byte[] code, int used, byte[] literals, int plan, int element,
            int receiver, int result, int chars, int index, int negative, int value, int work)
        {
            bool signed = (element & 1) == 0;
            used = Local(code, used, 0x20, receiver);
            if (element < 10)
            {
                if (element == 4 || element == 6)
                    code[used++] = element == 4 ? (byte)0xc0 : (byte)0xc1; // extend packed signed values
                else if (element < 8)
                {
                    used = Const(code, used, element == 5 ? 255 : 65535);
                    code[used++] = 0x71; // i32.and
                }
                code[used++] = signed ? (byte)0xac : (byte)0xad; // i64.extend_i32_s/u
            }
            used = Local(code, used, 0x21, value);

            code[used++] = 0x02; // block (result String)
            used = WasmBinary.ValueType(code, used, String);
            int count = Int(literals, plan);
            if (count != 0)
            {
                used = Local(code, used, 0x20, value);
                if (signed && element < 10)
                {
                    used = WideConst(code, used, element == 4 ? 255 : element == 6 ? 65535 : -1, 0);
                    code[used++] = 0x83; // i64.and: compare the unsigned bits of the declared width
                }
                used = Local(code, used, 0x21, work);
                int entry = plan + EnumFormatting.Header;
                for (int i = 0; i < count; i++, entry += EnumFormatting.Entry)
                {
                    used = Local(code, used, 0x20, work);
                    used = WideConst(code, used, Int(literals, entry), Int(literals, entry + 4));
                    code[used++] = 0x51; // i64.eq
                    used = Begin(code, used, 0x04);
                    used = StringWasm.LoadLiteral(code, used, Int(literals, entry + 16),
                        Int(literals, entry + 8), Int(literals, entry + 12));
                    code[used++] = 0x0c; code[used++] = 1; // return the cached name
                    code[used++] = 0x0b;
                }
            }

            int cache = Int(literals, plan + 4), vector = Int(literals, plan + 8);
            used = IsSmall(code, used, value);
            used = Begin(code, used, 0x04);
            used = Global(code, used, 0x23, cache);
            code[used++] = 0xd1; // ref.is_null
            used = Begin(code, used, 0x04);
            used = Const(code, used, EnumFormatting.CacheLimit);
            used = Gc(code, used, 7, vector); // array.new_default
            used = Global(code, used, 0x24, cache);
            code[used++] = 0x0b;
            used = CacheSlot(code, used, cache, value);
            used = Gc(code, used, 0x0b, vector); // array.get
            used = Local(code, used, 0x22, result);
            code[used++] = 0xd1; code[used++] = 0x45; // cached String exists
            used = Begin(code, used, 0x04);
            used = Local(code, used, 0x20, result);
            code[used++] = 0x0c; code[used++] = 2;
            code[used++] = 0x0b;
            code[used++] = 0x0b;

            used = Decimal(code, used, signed, chars, index, negative, value, work);
            used = Local(code, used, 0x21, result);
            used = IsSmall(code, used, value);
            used = Begin(code, used, 0x04);
            used = CacheSlot(code, used, cache, value);
            used = Local(code, used, 0x20, result);
            used = Gc(code, used, 0x0e, vector); // array.set
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, result);
            code[used++] = 0x0b;
            return Local(code, used, 0x21, result);
        }

        // [] -> [String] for the i64 in `value`: the invariant decimal text of a signed or
        // unsigned primitive (decision 0104). It shares the enum fallback's digit loops.
        internal static int FormatDecimal(byte[] code, int used, bool signed, int chars, int index, int negative, int value, int work) =>
            Decimal(code, used, signed, chars, index, negative, value, work);

        // Two passes allocate the exact immutable UTF-16 payload. Unsigned
        // magnitude arithmetic admits both long.MinValue and ulong.MaxValue.
        private static int Decimal(byte[] code, int used, bool signed, int chars, int index, int negative, int value, int work)
        {
            if (signed)
            {
                used = Local(code, used, 0x20, value);
                used = WideConst(code, used, 0, 0);
                code[used++] = 0x53; // i64.lt_s
                used = Local(code, used, 0x21, negative);
            }
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            used = Magnitude(code, used, signed, negative, value, work);
            used = Begin(code, used, 0x03);
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            used = NextDigit(code, used, work);
            used = Local(code, used, 0x20, index);
            if (signed)
            {
                used = Local(code, used, 0x20, negative);
                code[used++] = 0x6a;
                used = Local(code, used, 0x22, index);
            }
            used = Gc(code, used, 7, Chars);
            used = Local(code, used, 0x21, chars);

            used = Magnitude(code, used, signed, negative, value, work);
            used = Begin(code, used, 0x03);
            used = Local(code, used, 0x20, chars);
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, index);
            used = Local(code, used, 0x20, work);
            used = WideConst(code, used, 10, 0);
            code[used++] = 0x82; // i64.rem_u
            code[used++] = 0xa7; // i32.wrap_i64
            used = Const(code, used, 48);
            code[used++] = 0x6a;
            used = Gc(code, used, 0x0e, Chars);
            used = NextDigit(code, used, work);
            if (signed)
            {
                used = Local(code, used, 0x20, negative);
                used = Begin(code, used, 0x04);
                used = Local(code, used, 0x20, chars);
                used = Const(code, used, 0);
                used = Const(code, used, 45);
                used = Gc(code, used, 0x0e, Chars);
                code[used++] = 0x0b;
            }
            used = Local(code, used, 0x20, chars);
            return StringWasm.Wrap(code, used);
        }

        private static int Magnitude(byte[] code, int used, bool signed, int negative, int value, int work)
        {
            if (signed)
            {
                used = WideConst(code, used, 0, 0);
                used = Local(code, used, 0x20, value);
                code[used++] = 0x7d; // i64.sub
                used = Local(code, used, 0x20, value);
                used = Local(code, used, 0x20, negative);
                code[used++] = 0x1b; // select
            }
            else used = Local(code, used, 0x20, value);
            return Local(code, used, 0x21, work);
        }

        private static int NextDigit(byte[] code, int used, int work)
        {
            used = Local(code, used, 0x20, work);
            used = WideConst(code, used, 10, 0);
            code[used++] = 0x80; // i64.div_u
            used = Local(code, used, 0x22, work);
            code[used++] = 0x50; code[used++] = 0x45; // work != 0
            code[used++] = 0x0d; code[used++] = 0;
            code[used++] = 0x0b;
            return used;
        }

        private static int IsSmall(byte[] code, int used, int value)
        {
            used = Local(code, used, 0x20, value);
            used = WideConst(code, used, EnumFormatting.CacheLimit, 0);
            code[used++] = 0x54; // i64.lt_u also excludes sign-extended negatives
            return used;
        }

        private static int CacheSlot(byte[] code, int used, int global, int value)
        {
            used = Global(code, used, 0x23, global);
            used = Local(code, used, 0x20, value);
            code[used++] = 0xa7;
            return used;
        }

        private static int Begin(byte[] code, int used, byte instruction)
        {
            code[used++] = instruction;
            code[used++] = 0x40;
            return used;
        }

        private static int Global(byte[] code, int used, byte opcode, int index)
        {
            code[used++] = opcode;
            return WasmBinary.Unsigned(code, used, index);
        }

        private static int Int(byte[] bytes, int at) => unchecked((int)U32(bytes, (uint)at));
    }
}
