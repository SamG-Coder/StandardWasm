using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class RuntimeAssemblyMetadataWasm
    {
        internal static int Write(byte[] code, int used, int imports, RuntimeReflectionPlan reflection,
            RuntimeAssemblyMetadata query)
        {
            if (code.Length - used < 48) return -3;
            const int assembly = 0, slot = 1, vector = 2, attribute = 3;
            code[used++] = 3; // three local declaration groups
            code[used++] = 1;
            code[used++] = 0x7f; // assembly module slot
            code[used++] = 1;
            used = WasmBinary.ValueType(code, used, query.Vector);
            code[used++] = 1;
            used = WasmBinary.ValueType(code, used, query.Attribute);
            used = GuardNull(code, used, assembly, ExceptionTagsWasm.ArgumentNullTag);
            used = RuntimeReflectionWasm.ModuleSlot(code, used, 0, assembly, reflection);
            used = Local(code, used, 0x21, slot);

            for (int module = 1; module < query.Starts.Length - 1; module++)
            {
                if (code.Length - used < 44) return -3;
                int start = query.Starts[module], end = query.Starts[module + 1];
                used = Local(code, used, 0x20, slot);
                used = Const(code, used, module);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40; // if (void)
                used = WriteVector(code, used, (end - start) / RuntimeAssemblyMetadata.AttributeWidth, query);
                used = Local(code, used, 0x21, vector);
                for (int i = start; i < end; i += RuntimeAssemblyMetadata.AttributeWidth)
                {
                    if (code.Length - used < 90) return -3;
                    used = Local(code, used, 0x20, vector);
                    used = Const(code, used, (i - start) / RuntimeAssemblyMetadata.AttributeWidth);
                    used = Gc(code, used, 1, query.Attribute); // struct.new_default
                    used = Local(code, used, 0x22, attribute);
                    used = WriteString(code, used, query.Values[i], query.Values[i + 1], query.EmptyString);
                    used = WriteString(code, used, query.Values[i + 2], query.Values[i + 3], query.EmptyString);
                    code[used++] = 0x10; // call the original CoreLib constructor
                    used = WasmBinary.Unsigned(code, used, imports + query.Constructor);
                    used = Local(code, used, 0x20, attribute);
                    used = Gc(code, used, 14, query.Vector); // array.set
                }
                if (code.Length - used < 6) return -3;
                used = Local(code, used, 0x20, vector);
                code[used++] = 0x0f; // return
                code[used++] = 0x0b; // end if
            }
            if (code.Length - used < 2) return -3;
            code[used++] = 0x00; // Only known assembly descriptors can reach the helper.
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteVector(byte[] code, int used, int count, RuntimeAssemblyMetadata query)
        {
            if (count != 0 || query.EmptyVector < 0)
            {
                used = Const(code, used, count);
                return Gc(code, used, 7, query.Vector); // array.new_default
            }
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, query.EmptyVector);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40; // if (void)
            used = Const(code, used, 0);
            used = Gc(code, used, 7, query.Vector);
            code[used++] = 0x24; // global.set
            used = WasmBinary.Unsigned(code, used, query.EmptyVector);
            code[used++] = 0x0b;
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, query.EmptyVector);
        }

        private static int WriteString(byte[] code, int used, int offset, int length, int empty)
        {
            if (offset < 0)
            {
                code[used++] = 0xd0; // ref.null string
                return WasmBinary.Signed(code, used, WasmBinary.HeapType(String));
            }
            if (length == 0) return StringWasm.LoadLiteral(code, used, empty, 0, 0);
            used = Const(code, used, offset);
            used = Const(code, used, length);
            used = Gc(code, used, 9, Chars); // array.new_data
            code[used++] = 0; // literal data segment
            return StringWasm.Wrap(code, used);
        }
    }
}
