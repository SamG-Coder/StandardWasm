using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleWasm
    {
        internal const int BodyCapacity = 64;

        internal static int TypeBytes(int member) =>
            3 + Win32ModuleBinding.Arity(member) + (member == Win32ModuleBinding.ReleaseContext ? 0 : 1);

        internal static int ImportEntryBytes(int type, int member) =>
            Win32ErrorWasm.Module.Length + Win32ModuleBinding.EntryName(member).Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor, int member)
        {
            int arity = Win32ModuleBinding.Arity(member);
            output[cursor++] = 0x60;
            output[cursor++] = (byte)arity;
            for (int index = 0; index < arity; index++) output[cursor++] = 0x7f;
            bool hasResult = member != Win32ModuleBinding.ReleaseContext;
            output[cursor++] = hasResult ? (byte)1 : (byte)0;
            if (hasResult) output[cursor++] = 0x7f;
            return cursor;
        }

        internal static int WriteEntry(byte[] output, int cursor, int type, int import)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Win32ErrorWasm.Module);
            string name = Win32ModuleBinding.EntryName(import);
            cursor = Win32ErrorWasm.WriteName(output, cursor, name);
            // Win32 host imports use lower camel case.
            output[cursor - name.Length] = (byte)(name[0] + 32);
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }

        internal static int WriteBody(byte[] code, int used, int[] types, int shape, int member, int import, int hostImports, int initializer)
        {
            code[used++] = 0; // no declared locals
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + initializer);
            }
            int arity = Win32ModuleBinding.Arity(member);
            for (int parameter = 0; parameter < arity; parameter++)
            {
                int input = types[shape + parameter + 1];
                if (IsScalar(input)) used = Local(code, used, 0x20, parameter);
                else
                {
                    // Default one-field Win32 values carry a zero native field.
                    used = ValueFieldsWasm.Materialize(code, used, input, parameter);
                    used = Gc(code, used, 2, input);
                    code[used++] = 0;
                }
            }
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            int result = types[shape];
            if (result != Void && !IsScalar(result)) used = Gc(code, used, 0, result);
            code[used++] = 0x0b;
            return used;
        }
    }
}
