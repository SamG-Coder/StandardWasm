using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class NativeUtf8Wasm
    {
        private const int Address = 0, Length = 1, Read = 2, Written = 3, Lead = 4, Needed = 5,
            Scalar = 6, Value = 7, Part = 8, Low = 9, High = 10, Phase = 11, Output = 12;

        internal static int Write(byte[] code, int used, NativeUtf8Plan plan, int[] bodies, int[] sizes)
        {
            int function = plan.Decoder;
            if (function < 0) return used;
            if (code.Length - used < 600) return -3;
            bodies[function] = used;
            code[used++] = 2;
            code[used++] = 10; code[used++] = 0x7f;
            code[used++] = 1; code[used++] = 0x63; // nullable char[]
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(SignatureTypes.Chars));

            used = Get(code, used, Length);
            code[used++] = 0x45;
            used = Open(code, used, 0x04);
            used = StringWasm.LoadLiteral(code, used, plan.Empty, 0, 0);
            code[used++] = 0x0f; code[used++] = 0x0b;

            // Check a wasm32 byte range in i64 so the last legal end address
            // (4 GiB) cannot be confused with the start of memory by wrapping.
            used = Get(code, used, Address); code[used++] = 0xad;
            used = Get(code, used, Length); code[used++] = 0xad;
            code[used++] = 0x7c; // i64.add
            code[used++] = 0x3f; code[used++] = 0;
            code[used++] = 0xad;
            code[used++] = 0x42; code[used++] = 16;
            code[used++] = 0x86; // i64.shl
            code[used++] = 0x56; // i64.gt_u
            used = Get(code, used, Length);
            used = Op(code, used, 0, 0x48); // negative signed count
            code[used++] = 0x72; // i32.or
            used = Get(code, used, Address);
            code[used++] = 0x45;
            code[used++] = 0x72;
            used = Open(code, used, 0x04);
            code[used++] = 0x00; code[used++] = 0x0b;

            // First pass counts exact UTF-16 length. Second pass fills a
            // private GC array before publishing the immutable string.
            used = Open(code, used, 0x03);
            used = Open(code, used, 0x02);
            used = Open(code, used, 0x03);
            used = AtEnd(code, used, 1);
            used = ReadScalar(code, used);
            used = WriteScalar(code, used);
            used = Branch(code, used, false, 0);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = Get(code, used, Phase);
            used = Open(code, used, 0x04);
            used = StringWasm.Wrap(code, Get(code, used, Output));
            code[used++] = 0x0f; code[used++] = 0x0b;
            used = Gc(code, Get(code, used, Written), 7, SignatureTypes.Chars);
            used = Set(code, used, Output);
            used = Set(code, Const(code, used, 0), Read);
            used = Set(code, Const(code, used, 0), Written);
            used = Set(code, Const(code, used, 1), Phase);
            used = Branch(code, used, false, 0);
            code[used++] = 0x0b;
            code[used++] = 0x00; code[used++] = 0x0b;
            sizes[function] = used - bodies[function];
            return used;
        }

        internal static int WriteLength(byte[] code, int used, int function, int[] bodies, int[] sizes)
        {
            if (function < 0) return used;
            if (code.Length - used < 96) return -3;
            const int start = 0, length = 1, cursor = 2;
            bodies[function] = used;
            code[used++] = 1; code[used++] = 2; code[used++] = 0x7f;
            used = LinearMemoryWasm.GuardNull(code, used, start);
            used = Open(code, used, 0x03);
            used = Op(code, Get(code, used, length), int.MaxValue, 0x46);
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
            used = Get(code, Get(code, used, start), length);
            code[used++] = 0x6a;
            used = Local(code, used, 0x22, cursor);
            used = Get(code, used, start);
            code[used++] = 0x49; // i32.lt_u, reject a wrap at the wasm32 address-space boundary
            used = Open(code, used, 0x04);
            code[used++] = 0x00; code[used++] = 0x0b;
            used = LinearMemoryWasm.Load(code, Get(code, used, cursor), 0x47);
            code[used++] = 0x45;
            used = Open(code, used, 0x04);
            used = Get(code, used, length);
            code[used++] = 0x0f; code[used++] = 0x0b;
            used = Increment(code, used, length, 1);
            used = Branch(code, used, false, 0);
            code[used++] = 0x0b;
            code[used++] = 0x00; code[used++] = 0x0b;
            sizes[function] = used - bodies[function];
            return used;
        }
        private static int ReadScalar(byte[] code, int used)
        {
            used = Set(code, Const(code, used, 0xfffd), Scalar);
            used = ReadByte(code, used, Lead);
            used = Increment(code, used, Read, 1);
            used = Open(code, used, 0x02);
            used = Op(code, Get(code, used, Lead), 0x80, 0x49);
            used = Open(code, used, 0x04);
            used = Set(code, Get(code, used, Lead), Scalar);
            used = Branch(code, used, false, 1);
            code[used++] = 0x0b;
            used = Op(code, Get(code, used, Lead), 0xc2, 0x49);
            used = Op(code, Get(code, used, Lead), 0xf4, 0x4b);
            code[used++] = 0x72;
            used = Branch(code, used, true, 0);

            used = Const(code, used, 1);
            used = Const(code, used, 2);
            used = Const(code, used, 3);
            used = Op(code, Get(code, used, Lead), 0xf0, 0x49);
            code[used++] = 0x1b;
            used = Op(code, Get(code, used, Lead), 0xe0, 0x49);
            code[used++] = 0x1b;
            used = Set(code, used, Needed);
            used = Get(code, used, Lead);
            used = Const(code, used, 127);
            used = Op(code, Get(code, used, Needed), 1, 0x6a);
            code[used++] = 0x76; // i32.shr_u
            code[used++] = 0x71; // i32.and
            used = Set(code, used, Value);
            used = RangeBound(code, used, Low, 0x80, 0xe0, 0xa0, 0xf0, 0x90);
            used = RangeBound(code, used, High, 0xbf, 0xed, 0x9f, 0xf4, 0x8f);

            // Consume only the valid prefix. The first non-continuation is
            // processed again by the outer loop, matching .NET replacement.
            used = Open(code, used, 0x03);
            used = AtEnd(code, used, 1);
            used = ReadByte(code, used, Part);
            used = Get(code, Get(code, used, Part), Low);
            code[used++] = 0x49;
            used = Get(code, Get(code, used, Part), High);
            code[used++] = 0x4b;
            code[used++] = 0x72;
            used = Branch(code, used, true, 1);
            used = Increment(code, used, Read, 1);
            used = Op(code, Get(code, used, Value), 6, 0x74);
            used = Op(code, Get(code, used, Part), 63, 0x71);
            code[used++] = 0x72;
            used = Set(code, used, Value);
            used = Set(code, Const(code, used, 0x80), Low);
            used = Set(code, Const(code, used, 0xbf), High);
            used = Op(code, Get(code, used, Needed), 1, 0x6b);
            used = Local(code, used, 0x22, Needed);
            used = Branch(code, used, true, 0);
            code[used++] = 0x0b;
            used = Set(code, Get(code, used, Value), Scalar);
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteScalar(byte[] code, int used)
        {
            used = Op(code, Get(code, used, Scalar), 0x10000, 0x4f);
            used = Open(code, used, 0x04);
            used = Set(code, Op(code, Get(code, used, Scalar), 0x10000, 0x6b), Scalar);
            used = Get(code, used, Phase);
            used = Open(code, used, 0x04);
            used = Get(code, Get(code, used, Output), Written);
            used = Op(code, Get(code, used, Scalar), 10, 0x76);
            used = Op(code, used, 0xd800, 0x72);
            used = Gc(code, used, 14, SignatureTypes.Chars); // array.set
            code[used++] = 0x0b;
            used = Increment(code, used, Written, 1);
            used = Op(code, Get(code, used, Scalar), 1023, 0x71);
            used = Set(code, Op(code, used, 0xdc00, 0x72), Scalar);
            code[used++] = 0x0b;
            used = Get(code, used, Phase);
            used = Open(code, used, 0x04);
            used = Get(code, Get(code, Get(code, used, Output), Written), Scalar);
            used = Gc(code, used, 14, SignatureTypes.Chars);
            code[used++] = 0x0b;
            return Increment(code, used, Written, 1);
        }

        private static int RangeBound(byte[] code, int used, int target, int fallback, int first, int firstBound,
            int second, int secondBound)
        {
            used = Const(code, Const(code, Const(code, used, firstBound), secondBound), fallback);
            used = Op(code, Get(code, used, Lead), second, 0x46);
            code[used++] = 0x1b; // select
            used = Op(code, Get(code, used, Lead), first, 0x46);
            code[used++] = 0x1b;
            return Set(code, used, target);
        }

        private static int ReadByte(byte[] code, int used, int target)
        {
            used = Get(code, Get(code, used, Address), Read);
            code[used++] = 0x6a;
            return Set(code, LinearMemoryWasm.Load(code, used, 0x47), target);
        }

        private static int AtEnd(byte[] code, int used, byte label)
        {
            used = Get(code, Get(code, used, Read), Length);
            code[used++] = 0x46;
            return Branch(code, used, true, label);
        }

        private static int Increment(byte[] code, int used, int local, int amount) =>
            Set(code, Op(code, Get(code, used, local), amount, 0x6a), local);

        private static int Get(byte[] code, int used, int index) => Local(code, used, 0x20, index);
        private static int Set(byte[] code, int used, int index) => Local(code, used, 0x21, index);

        private static int Op(byte[] code, int used, int immediate, byte op)
        {
            used = Const(code, used, immediate);
            code[used++] = op;
            return used;
        }

        private static int Open(byte[] code, int used, byte op)
        {
            code[used++] = op; code[used++] = 0x40;
            return used;
        }

        private static int Branch(byte[] code, int used, bool conditional, byte label)
        {
            code[used++] = conditional ? (byte)0x0d : (byte)0x0c;
            code[used++] = label;
            return used;
        }
    }
}
