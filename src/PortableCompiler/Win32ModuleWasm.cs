using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleWasm
    {
        internal const int BodyCapacity = 64, DpiQueryBodyCapacity = 112;

        private static int ResultCount(int member) => member == Win32ModuleBinding.ReleaseContext ? 0 :
            member == Win32ModuleBinding.GetProcessAwareness ? 2 : 1;

        internal static int TypeBytes(int member) => 3 + Win32ModuleBinding.Arity(member) + ResultCount(member);

        internal static int ImportEntryBytes(int type, int member) =>
            Win32ErrorWasm.Module.Length + Win32ModuleBinding.EntryName(member).Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor, int member)
        {
            int arity = Win32ModuleBinding.Arity(member);
            output[cursor++] = 0x60;
            output[cursor++] = (byte)arity;
            for (int index = 0; index < arity; index++) output[cursor++] = 0x7f;
            int results = ResultCount(member);
            output[cursor++] = (byte)results;
            for (int index = 0; index < results; index++) output[cursor++] = 0x7f;
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

        internal static int WriteBody(byte[] code, int used, int[] types, int shape, int member, int import,
            int hostImports, int initializer, int errorBase)
        {
            if (member == Win32ModuleBinding.GetProcessAwareness)
                return WriteDpiQuery(code, used, types, shape, import, hostImports, initializer);
            code[used++] = 0; // no declared locals
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + initializer);
            }
            if (Win32ModuleBinding.UsesLastError(member))
            {
                used = Const(code, used, 0);
                used = ErrorCall(code, used, errorBase, MarshalErrorMetadata.SetSystem);
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
            if (Win32ModuleBinding.UsesLastError(member))
            {
                // The native return value stays below the error value on the stack.
                used = ErrorCall(code, used, errorBase, MarshalErrorMetadata.GetSystem);
                used = ErrorCall(code, used, errorBase, MarshalErrorMetadata.SetPInvoke);
            }
            int result = types[shape];
            if (result != Void && !IsScalar(result)) used = Gc(code, used, 0, result);
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteDpiQuery(byte[] code, int used, int[] types, int shape, int import,
            int hostImports, int initializer)
        {
            // The browser host returns [HRESULT, awareness]. It never receives
            // a managed pointer. The exact SDK wrapper writes the result into
            // its original cell, including -1 on an invalid-handle failure.
            code[used++] = 1; // one local group
            code[used++] = 2;
            code[used++] = 0x7f;
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + initializer);
            }
            int handle = types[shape + 1];
            if (handle == Native) used = Local(code, used, 0x20, 0);
            else
            {
                used = ValueFieldsWasm.Materialize(code, used, handle, 0);
                used = Gc(code, used, 2, handle);
                code[used++] = 0;
            }
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            used = Local(code, used, 0x21, 3);
            used = Local(code, used, 0x21, 2);
            used = ManagedLocationWasm.AccessElement(code, used, 1, Ints);
            used = Local(code, used, 0x20, 3);
            used = Gc(code, used, 0x0e, Ints); // array.set
            used = Local(code, used, 0x20, 2);
            int result = types[shape];
            if (result != I32) used = Gc(code, used, 0, result);
            code[used++] = 0x0b;
            return used;
        }

        private static int ErrorCall(byte[] code, int used, int errorBase, int member)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, errorBase + member - 1);
        }
    }
}
