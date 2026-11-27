namespace PortableCompiler
{
    internal static class StringWasm
    {
        internal static int LoadLiteral(byte[] code, int used, int global, int offset, int length)
        {
            // The passive data segment survives for the lifetime of
            // this module. Allocate each immutable literal on demand.
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, global);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = AllocateLiteral(code, used, offset, length);
            code[used++] = 0x24; // global.set
            used = WasmBinary.Unsigned(code, used, global);
            code[used++] = 0x0b;
            code[used++] = 0x23; // global.get
            return WasmBinary.Unsigned(code, used, global);
        }

        internal static int AllocateLiteral(byte[] code, int used, int offset, int length)
        {
            if (length != 0)
            {
                used = BranchWasm.Const(code, used, offset);
                used = BranchWasm.Const(code, used, length);
            }
            code[used++] = 0xfb;
            code[used++] = length == 0 ? (byte)8 : (byte)9; // fixed empty or passive data
            code[used++] = 4;
            code[used++] = 0;
            return Wrap(code, used);
        }

        // Three private Char[] scratch locals followed by two lengths. The
        // published String only exposes its immutable reference to a fresh copy.
        internal static int Copy(byte[] code, int used, int left, int right, int scratch, bool join, int empty)
        {
            int a = scratch, b = scratch + 1, output = scratch + 2;
            int aLength = scratch + 3, bLength = scratch + 4;
            used = ArrayOrEmpty(code, used, left, join);
            used = Local(code, used, 0x21, a);
            used = Length(code, used, a);
            used = Local(code, used, 0x21, aLength);
            if (join)
            {
                used = ArrayOrEmpty(code, used, right, true);
                used = Local(code, used, 0x21, b);
                used = Length(code, used, b);
                used = Local(code, used, 0x21, bLength);
            }

            code[used++] = 0x02; // block (result String)
            used = WasmBinary.ValueType(code, used, SignatureTypes.String);
            used = IfEmpty(code, used, aLength);
            if (join)
            {
                // Concat returns the other nonempty argument as-is.
                used = Local(code, used, 0x20, bLength);
                code[used++] = 0x04; // if (result String)
                used = WasmBinary.ValueType(code, used, SignatureTypes.String);
                used = Local(code, used, 0x20, right);
                code[used++] = 0x05; // else
            }
            used = LoadLiteral(code, used, empty, 0, 0);
            if (join) code[used++] = 0x0b;
            used = ReturnString(code, used);
            if (join)
            {
                used = IfEmpty(code, used, bLength);
                used = Local(code, used, 0x20, left);
                used = ReturnString(code, used);
            }

            used = Local(code, used, 0x20, aLength);
            if (join)
            {
                used = Local(code, used, 0x20, bLength);
                code[used++] = 0x6a;
            }
            code[used++] = 0xfb;
            code[used++] = 7; // array.new_default Char[]
            code[used++] = 4;
            used = Local(code, used, 0x21, output);
            used = CopyArray(code, used, output, -1, a, aLength);
            if (join)
                used = CopyArray(code, used, output, aLength, b, bLength);
            used = Local(code, used, 0x20, output);
            used = Wrap(code, used);
            code[used++] = 0x0b; // end result block
            return used;
        }

        private static int IfEmpty(byte[] code, int used, int length)
        {
            used = Local(code, used, 0x20, length);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; // if
            code[used++] = 0x40;
            return used;
        }

        private static int ReturnString(byte[] code, int used)
        {
            code[used++] = 0x0c; // br to the outer result block with a String
            code[used++] = 1;
            code[used++] = 0x0b; // end if
            return used;
        }

        // Compare the UTF-16 code units exactly. Equal references include
        // the null/null case and need no character access.
        internal static int OrdinalEquals(byte[] code, int used, int left, int right, int result, int scratch)
        {
            int a = scratch, b = scratch + 1, length = scratch + 3, index = scratch + 4;
            used = Zero(code, used);
            used = Local(code, used, 0x21, result);
            code[used++] = 0x02; code[used++] = 0x40; // comparison block
            used = Local(code, used, 0x20, left);
            used = Local(code, used, 0x20, right);
            code[used++] = 0xd3; // ref.eq
            code[used++] = 0x04; code[used++] = 0x40;
            used = SetTrue(code, used, result);
            code[used++] = 0x0c; code[used++] = 1; // equal
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, left);
            code[used++] = 0xd1; // ref.is_null
            used = Local(code, used, 0x20, right);
            code[used++] = 0xd1;
            code[used++] = 0x72; // i32.or
            code[used++] = 0x0d; code[used++] = 0; // unequal

            used = Local(code, used, 0x20, left);
            used = Unwrap(code, used);
            used = Local(code, used, 0x21, a);
            used = Local(code, used, 0x20, right);
            used = Unwrap(code, used);
            used = Local(code, used, 0x21, b);
            used = Length(code, used, a);
            used = Local(code, used, 0x22, length);
            used = Length(code, used, b);
            code[used++] = 0x47; // i32.ne
            code[used++] = 0x0d; code[used++] = 0; // unequal lengths
            used = Zero(code, used);
            used = Local(code, used, 0x21, index);

            code[used++] = 0x03; code[used++] = 0x40; // compare each unit
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x04; code[used++] = 0x40;
            used = SetTrue(code, used, result);
            code[used++] = 0x0c; code[used++] = 2; // equal
            code[used++] = 0x0b;
            used = Character(code, used, a, index);
            used = Character(code, used, b, index);
            code[used++] = 0x47;
            code[used++] = 0x0d; code[used++] = 1; // unequal
            used = Local(code, used, 0x20, index);
            code[used++] = 0x41; code[used++] = 1;
            code[used++] = 0x6a; // i32.add
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // comparison
            return used;
        }

        internal static int ContainsCodeUnit(byte[] code, int used, int source, int needle, int result, int scratch)
        {
            int array = scratch, length = scratch + 3, index = scratch + 4;
            used = Unwrap(code, Local(code, used, 0x20, source));
            used = Local(code, used, 0x21, array);
            used = Local(code, Length(code, used, array), 0x21, length);
            used = Local(code, Zero(code, used), 0x21, result);
            used = Local(code, Zero(code, used), 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // search block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x0d; code[used++] = 1; // exhausted

            // Equality is on one UTF-16 unit, including isolated surrogate units.
            used = Character(code, used, array, index);
            used = Local(code, used, 0x20, needle);
            code[used++] = 0x46; // i32.eq
            used = Local(code, used, 0x22, result);
            code[used++] = 0x0d; code[used++] = 1; // found
            used = Local(code, used, 0x20, index);
            used = BranchWasm.Const(code, used, 1);
            code[used++] = 0x6a; // i32.add
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int SetTrue(byte[] code, int used, int result)
        {
            code[used++] = 0x41; code[used++] = 1;
            return Local(code, used, 0x21, result);
        }

        private static int Character(byte[] code, int used, int array, int index)
        {
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, index);
            code[used++] = 0xfb;
            code[used++] = 0x0d; // array.get_u on packed Char[]
            code[used++] = 4;
            return used;
        }

        internal static int Wrap(byte[] code, int used)
        {
            code[used++] = 0xfb;
            code[used++] = 0;
            code[used++] = 5; // struct.new String
            return used;
        }

        internal static int AllocateForConstruction(byte[] code, int used, int length)
        {
            used = Local(code, used, 0x20, length);
            return Wrap(code, BranchWasm.Gc(code, used, 7, SignatureTypes.Chars));
        }

        internal static int OpenConstructionSpan(byte[] code, int used, int value, int length)
        {
            used = Unwrap(code, Local(code, used, 0x20, value));
            used = Zero(code, used);
            used = Local(code, used, 0x20, length);
            return BranchWasm.Gc(code, used, 0, SignatureTypes.WritableCharacterSpan);
        }

        internal static int CopyRange(byte[] code, int used, int source, int start, int length, int scratch)
        {
            // The caller checked the range. Initialize the new UTF-16 owner
            // before constructing the publishable String wrapper.
            used = Local(code, used, 0x20, length);
            used = BranchWasm.Gc(code, used, 7, SignatureTypes.Chars); // array.new_default
            used = Local(code, used, 0x22, scratch);
            used = Zero(code, used);
            used = Unwrap(code, Local(code, used, 0x20, source));
            used = Local(code, used, 0x20, start);
            used = Local(code, used, 0x20, length);
            used = BranchWasm.Gc(code, used, 0x11, SignatureTypes.Chars); // array.copy
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(SignatureTypes.Chars));
            return Wrap(code, Local(code, used, 0x20, scratch));
        }

        internal static int CopySpan(byte[] code, int used, int span, int empty, int scratch, bool nativeCharacters)
        {
            int output = scratch + 2, length = scratch + 3, index = scratch + 4;
            used = Local(code, SpanWasm.Length(code, used, span), 0x21, length);
            code[used++] = 0x02; // block (result String)
            used = WasmBinary.ValueType(code, used, SignatureTypes.String);
            used = IfEmpty(code, used, length);
            used = LoadLiteral(code, used, empty, 0, 0);
            used = ReturnString(code, used);

            used = Local(code, used, 0x20, length);
            used = BranchWasm.Gc(code, used, 7, SignatureTypes.Chars);
            used = Local(code, used, 0x21, output);
            if (nativeCharacters)
            {
                // A nonempty descriptor with a null owner addresses linear memory.
                used = SpanWasm.LoadField(code, used, span, 0);
                code[used++] = 0xd1;
                code[used++] = 0x04; code[used++] = 0x40;
                used = CopyNativeSpan(code, used, span, output, length, index);
                code[used++] = 0x05;
            }
            used = Local(code, used, 0x20, output);
            used = Zero(code, used);
            used = SpanWasm.LoadField(code, used, span, 0);
            code[used++] = 0xfb; code[used++] = 0x16; // ref.cast non-null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(SignatureTypes.Chars));
            used = SpanWasm.LoadField(code, used, span, 1);
            used = Local(code, used, 0x20, length);
            used = BranchWasm.Gc(code, used, 0x11, SignatureTypes.Chars);
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(SignatureTypes.Chars));
            if (nativeCharacters) code[used++] = 0x0b;

            used = Wrap(code, Local(code, used, 0x20, output));
            code[used++] = 0x0b;
            return used;
        }

        private static int CopyNativeSpan(byte[] code, int used, int span, int output, int length, int index)
        {
            // Read exact UTF-16 units before publishing an immutable string.
            // The span length bounds the loop and each byte offset is scaled by 2.
            used = NativeCharacterViewWasm.GuardBase(code, used, span);
            used = Local(code, Zero(code, used), 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46;
            code[used++] = 0x0d; code[used++] = 1;
            used = Local(code, used, 0x20, output);
            used = Local(code, used, 0x20, index);
            used = NativeCharacterViewWasm.ReadWithinSpan(code, used, span, index);
            used = BranchWasm.Gc(code, used, 0x0e, SignatureTypes.Chars);
            used = Local(code, used, 0x20, index);
            used = BranchWasm.Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        internal static int BorrowCharacters(byte[] code, int used, int reference)
        {
            used = BranchWasm.GuardNull(code, used, reference);
            used = Unwrap(code, Local(code, used, 0x20, reference));
            used = Zero(code, used);
            return BranchWasm.Gc(code, used, 0, SignatureTypes.ReadOnlyCharacterLocation);
        }

        internal static int FirstCodeUnit(byte[] code, int used, int reference)
        {
            used = Unwrap(code, Local(code, used, 0x20, reference));
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x7f;
            // CoreLib's first field contains the terminator for an empty string.
            used = Zero(code, used);
            code[used++] = 0x05;
            used = Unwrap(code, Local(code, used, 0x20, reference));
            used = Zero(code, used);
            code[used++] = 0xfb; code[used++] = 0x0d; code[used++] = 4; // array.get_u Char[]
            code[used++] = 0x0b;
            return used;
        }

        internal static int Unwrap(byte[] code, int used)
        {
            code[used++] = 0xfb;
            code[used++] = 2;
            code[used++] = 5;
            code[used++] = 0;
            return used;
        }

        private static int ArrayOrEmpty(byte[] code, int used, int input, bool wrapped)
        {
            used = Local(code, used, 0x20, input);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            code[used++] = 0x63; // if result (ref null Char[])
            code[used++] = 4;
            code[used++] = 0xfb;
            code[used++] = 8; // empty array.new_fixed
            code[used++] = 4;
            code[used++] = 0;
            code[used++] = 0x05;
            used = Local(code, used, 0x20, input);
            if (wrapped) used = Unwrap(code, used);
            code[used++] = 0x0b;
            return used;
        }

        private static int Length(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xfb;
            code[used++] = 0x0f;
            return used;
        }

        private static int CopyArray(byte[] code, int used, int destination, int offset, int source, int length)
        {
            used = Local(code, used, 0x20, destination);
            used = offset < 0 ? Zero(code, used) : Local(code, used, 0x20, offset);
            used = Local(code, used, 0x20, source);
            used = Zero(code, used);
            used = Local(code, used, 0x20, length);
            code[used++] = 0xfb;
            code[used++] = 0x11; // array.copy
            code[used++] = 4;
            code[used++] = 4;
            return used;
        }

        internal static int CopyForConstruction(byte[] code, int used, int destination, int offset, int source, int scratch)
        {
            int targetArray = scratch, sourceArray = scratch + 1, count = scratch + 3;
            used = Local(code, Unwrap(code, Local(code, used, 0x20, destination)), 0x21, targetArray);
            used = Local(code, Unwrap(code, Local(code, used, 0x20, source)), 0x21, sourceArray);
            used = Local(code, Length(code, used, sourceArray), 0x21, count);
            return CopyArray(code, used, targetArray, offset, sourceArray, count);
        }

        private static int Zero(byte[] code, int used)
        {
            code[used++] = 0x41;
            code[used++] = 0;
            return used;
        }

        private static int Local(byte[] code, int used, byte instruction, int local)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, local);
        }
    }
}
