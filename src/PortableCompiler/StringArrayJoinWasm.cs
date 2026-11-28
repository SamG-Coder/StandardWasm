using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // String.Concat(string[]) (decision 0104), following the CoreLib body:
    //   null array          -> ArgumentNullException
    //   zero elements       -> String.Empty
    //   one element         -> that element itself, or String.Empty for null (no copy)
    //   otherwise           -> null elements count as empty; a zero total returns String.Empty,
    //                          and any other total allocates one fresh string.
    // CLR-to-Wasm: a total above Int32.MaxValue throws OutOfMemoryException on the CLR. Here it
    // traps (`unreachable`); no engine can allocate the inputs that would reach it.
    internal static class StringArrayJoinWasm
    {
        internal static int Emit(byte[] code, int used, int vector, int input, int empty,
            int chars, int element, int count, int index, int total, int position)
        {
            code[used++] = 0x02; // block (result String)
            used = WasmBinary.ValueType(code, used, SignatureTypes.String);

            used = Local(code, used, 0x20, input);
            code[used++] = 0xd1; // ref.is_null
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentNullTag);

            used = Local(code, used, 0x20, input);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, count);
            code[used++] = 0x45; // i32.eqz
            used = Begin(code, used);
            used = StringWasm.LoadLiteral(code, used, empty, 0, 0);
            code[used++] = 0x0c; code[used++] = 1; // return String.Empty
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, count);
            used = Const(code, used, 1);
            code[used++] = 0x46; // i32.eq
            used = Begin(code, used);
            used = ElementAt(code, used, vector, input, -1);
            code[used++] = 0xd1;
            code[used++] = 0x04; // if (result String): values[0] ?? String.Empty
            used = WasmBinary.ValueType(code, used, SignatureTypes.String);
            used = StringWasm.LoadLiteral(code, used, empty, 0, 0);
            code[used++] = 0x05;
            used = ElementAt(code, used, vector, input, -1);
            code[used++] = 0x0b;
            code[used++] = 0x0c; code[used++] = 1;
            code[used++] = 0x0b;

            // First pass: the total length in i64, so the sum of Int32 lengths cannot wrap.
            used = WideConst(code, used, 0, 0);
            used = Local(code, used, 0x21, total);
            used = Each(code, used, vector, input, count, index, element, false, total, chars, position);

            used = Local(code, used, 0x20, total);
            used = WideConst(code, used, int.MaxValue, 0);
            code[used++] = 0x56; // i64.gt_u
            used = Begin(code, used);
            code[used++] = 0x00; // unreachable: the CLR throws OutOfMemoryException
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, total);
            code[used++] = 0x50; // i64.eqz
            used = Begin(code, used);
            used = StringWasm.LoadLiteral(code, used, empty, 0, 0);
            code[used++] = 0x0c; code[used++] = 1;
            code[used++] = 0x0b;

            // Second pass: copy each non-null element after the previous ones.
            used = Local(code, used, 0x20, total);
            code[used++] = 0xa7; // i32.wrap_i64
            used = Gc(code, used, 7, SignatureTypes.Chars); // array.new_default
            used = Local(code, used, 0x21, chars);
            used = WideConst(code, used, 0, 0);
            used = Local(code, used, 0x21, position);
            used = Each(code, used, vector, input, count, index, element, true, total, chars, position);

            used = Local(code, used, 0x20, chars);
            used = StringWasm.Wrap(code, used);
            code[used++] = 0x0b; // end result block
            return used;
        }

        // for (index = 0; index < count; index++) if (values[index] != null) { measure or copy }
        private static int Each(byte[] code, int used, int vector, int input, int count, int index, int element,
            bool copy, int total, int chars, int position)
        {
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, count);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1; // br_if out of the loop

            used = ElementAt(code, used, vector, input, index);
            code[used++] = 0xd1;
            code[used++] = 0x45; // non-null element
            used = Begin(code, used);
            used = ElementAt(code, used, vector, input, index);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x21, element);
            if (copy)
            {
                used = Local(code, used, 0x20, chars);
                used = Local(code, used, 0x20, position);
                code[used++] = 0xa7;
                used = Local(code, used, 0x20, element);
                used = Const(code, used, 0);
                used = Local(code, used, 0x20, element);
                code[used++] = 0xfb; code[used++] = 0x0f; // array.len
                used = Gc(code, used, 0x11, SignatureTypes.Chars); // array.copy Char[] <- Char[]
                used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(SignatureTypes.Chars));
                used = Advance(code, used, position, element);
            }
            else used = Advance(code, used, total, element);
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0; // continue
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block
            return used;
        }

        private static int Advance(byte[] code, int used, int counter, int element)
        {
            used = Local(code, used, 0x20, counter);
            used = Local(code, used, 0x20, element);
            code[used++] = 0xfb; code[used++] = 0x0f;
            code[used++] = 0xad; // i64.extend_i32_u
            code[used++] = 0x7c; // i64.add
            return Local(code, used, 0x21, counter);
        }

        // [] -> [String or null]: values[index], or values[0] when index is negative.
        private static int ElementAt(byte[] code, int used, int vector, int input, int index)
        {
            used = Local(code, used, 0x20, input);
            used = index < 0 ? Const(code, used, 0) : Local(code, used, 0x20, index);
            return Gc(code, used, 0x0b, vector); // array.get
        }

        private static int Begin(byte[] code, int used)
        {
            code[used++] = 0x04; code[used++] = 0x40;
            return used;
        }
    }
}
