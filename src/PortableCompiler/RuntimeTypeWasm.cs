using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class RuntimeTypeWasm
    {
        internal const int DescriptorBytes = 6;

        internal static bool HasDescriptors(int[] globals)
        {
            for (int i = 0; i < globals[0]; i++)
                if (RuntimeTypeTokens.IsDescriptorOwner(StaticFields.Owner(globals, i))) return true;
            return false;
        }

        internal static int WriteDescriptor(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 2;
            output[cursor++] = 0x7f; // defining module slot
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // TypeDef row or closed layout key
            output[cursor++] = 0;
            return cursor;
        }

        internal static int InitializerSize(int heap, int module, int row) =>
            4 + WasmBinary.UnsignedSize(module * 2) + WasmBinary.UnsignedSize(row * 2) + WasmBinary.UnsignedSize(heap);

        internal static int WriteInitializer(byte[] output, int cursor, int heap, int module, int row)
        {
            output[cursor++] = 0x41;
            cursor = WasmBinary.Signed(output, cursor, module);
            output[cursor++] = 0x41;
            cursor = WasmBinary.Signed(output, cursor, row);
            // The immutable global allocates the descriptor once per type and module instance.
            output[cursor++] = 0xfb;
            output[cursor++] = 0; // struct.new
            return WasmBinary.Unsigned(output, cursor, heap);
        }

        internal static int Cast(byte[] code, int used, int bodyStart, int source, int target, bool proven, bool throwing,
            RuntimeReflectionPlan reflection)
        {
            if (proven)
            {
                used = Local(code, used, 0x20, source);
                return Local(code, used, 0x21, target);
            }

            if (throwing)
            {
                // Null is valid for castclass. Read the source before updating
                // the result, since the two stack values can share a spill.
                used = Local(code, used, 0x20, source);
                code[used++] = 0xd1; // ref.is_null
                code[used++] = 0x45; // i32.eqz
                code[used++] = 0x04; // if (void)
                code[used++] = 0x40;
            }
            used = Local(code, used, 0x20, source);
            code[used++] = 0x10;
            used = WasmIndexFixups.Write(code, used, bodyStart, reflection.RuntimeTypeCastFixups);
            used = Local(code, used, throwing ? (byte)0x22 : (byte)0x21, target);
            if (throwing)
            {
                code[used++] = 0xd1;
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.InvalidCastTag);
                code[used++] = 0x05;
                used = NullResult(code, used);
                used = Local(code, used, 0x21, target);
                code[used++] = 0x0b;
            }
            return used;
        }

        internal static int WriteCastHelper(byte[] code, int used, int[] globals)
        {
            if (code.Length - used < 4) return -3;
            code[used++] = 0; // No locals.
            for (int global = 0; global < globals[0]; global++)
            {
                // Module and Assembly share the descriptor heap. Value-type
                // layouts can also be structurally equal to that heap.
                // Only canonical Type objects satisfy the sealed CLR class.
                if (!RuntimeTypeTokens.IsDescriptorOwner(StaticFields.Owner(globals, global)) ||
                    StaticFields.Row(globals, global) < 0) continue;
                if (code.Length - used < 18) return -3;
                used = Local(code, used, 0x20, 0);
                code[used++] = 0x23; // global.get
                used = WasmBinary.Unsigned(code, used, global);
                code[used++] = 0xd3; // ref.eq
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Local(code, used, 0x20, 0);
                code[used++] = 0x0f; // return original identity
                code[used++] = 0x0b;
            }
            if (code.Length - used < 3) return -3;
            used = NullResult(code, used);
            code[used++] = 0x0b;
            return used;
        }

        private static int NullResult(byte[] code, int used)
        {
            code[used++] = 0xd0; // ref.null eq
            code[used++] = 0x6d;
            return used;
        }
    }
}
