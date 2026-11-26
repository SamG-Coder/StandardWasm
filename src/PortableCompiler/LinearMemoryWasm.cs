namespace PortableCompiler
{
    // Stage 132 (decision 0060). Unmanaged pointers address memory 0, one wasm32
    // memory lent by the host. The import entry is the one Stage 131's resource
    // host also uses, so a module never has two memories. A memory import does
    // not occupy a function index, so console and managed call indices are unchanged.
    internal static class LinearMemoryWasm
    {
        private const string ModuleName = "standardwasm:resources", FieldName = "memory";

        internal static int ImportBytes =>
            1 + ModuleName.Length + 1 + FieldName.Length + 1 + 1 + 1;

        internal static int WriteImport(byte[] output, int cursor)
        {
            cursor = Name(output, cursor, ModuleName);
            cursor = Name(output, cursor, FieldName);
            output[cursor++] = 2; // memory
            output[cursor++] = 0; // wasm32 limits with no maximum
            output[cursor++] = 1; // at least one page
            return cursor;
        }

        // Null is address 0 on the CLR and here: dereferencing it raises the typed
        // NullReferenceException. Any other address outside the memory is an engine
        // trap, standing in for the CLR's access violation, never a typed exception.
        internal static int GuardNull(byte[] code, int used, int address)
        {
            used = BranchWasm.Local(code, used, 0x20, address);
            code[used++] = 0x45; // i32.eqz
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
        }

        // ldind.* opcode -> Wasm load opcode. The memarg has no alignment promise
        // (C# pointers need not be aligned) and offset zero.
        internal static int Load(byte[] code, int used, int opcode, int offset = 0)
        {
            // ldind.i1/u1/i2/u2 -> i32.load8_s/8_u/16_s/16_u; ldind.i8 -> i64.load;
            // ldind.r4/r8 -> f32/f64.load; ldind.i4, ldind.u4, ldind.i -> i32.load.
            code[used++] = (byte)(opcode == 0x46 ? 0x2c : opcode == 0x47 ? 0x2d : opcode == 0x48 ? 0x2e :
                opcode == 0x49 ? 0x2f : opcode == 0x4c ? 0x29 : opcode == 0x4e ? 0x2a : opcode == 0x4f ? 0x2b : 0x28);
            return MemoryArgument(code, used, offset);
        }

        internal static int Store(byte[] code, int used, int opcode, int offset = 0)
        {
            // stind.i1/i2 -> i32.store8/16; stind.i8 -> i64.store; stind.r4/r8 ->
            // f32/f64.store; stind.i4, stind.i -> i32.store.
            code[used++] = (byte)(opcode == 0x52 ? 0x3a : opcode == 0x53 ? 0x3b : opcode == 0x55 ? 0x37 :
                opcode == 0x56 ? 0x38 : opcode == 0x57 ? 0x39 : 0x36);
            return MemoryArgument(code, used, offset);
        }

        private static int MemoryArgument(byte[] code, int used, int offset)
        {
            code[used++] = 0; // alignment 2^0
            return WasmBinary.Unsigned(code, used, offset);
        }

        private static int Name(byte[] output, int cursor, string name)
        {
            output[cursor++] = (byte)name.Length;
            for (int i = 0; i < name.Length; i++) output[cursor++] = (byte)name[i];
            return cursor;
        }
    }
}
