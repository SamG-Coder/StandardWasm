using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ReferenceFieldWasm
    {
        internal static int Emit(byte[] code, int used, int kind, int descriptor, int field,
            int vector, int receiver, int source, int value, int result, int resultType)
        {
            int owner = FieldAccess.Owner(descriptor);
            int storage = FieldAccess.Storage(descriptor);
            used = GuardNull(code, used, receiver);
            used = Cell(code, used, owner, receiver, field);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            if (kind == GetReferenceField)
            {
                used = WasmBinary.ValueType(code, used, storage);
                code[used++] = 0xd0; // An untouched field has its CLR default.
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(storage));
                code[used++] = 0x05;
                used = Cell(code, used, owner, receiver, field);
                used = Const(code, used, 0);
                used = Gc(code, used, 0x0b, vector); // array.get
                used = ManagedLocationWasm.CastCellValue(code, used, storage);
                code[used++] = 0x0b;
                return Local(code, used, 0x21, result);
            }

            // Install [payload, owner] on the first write or address-taking use.
            // The second element keeps the containing object alive through a byref.
            code[used++] = 0x40;
            used = Local(code, used, 0x20, receiver);
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = Local(code, used, 0x20, receiver);
            used = Gc(code, used, 8, vector); // array.new_fixed
            code[used++] = 2;
            used = Gc(code, used, 5, owner);
            used = WasmBinary.Unsigned(code, used, field);
            code[used++] = 0x0b;
            used = Cell(code, used, owner, receiver, field);
            used = Const(code, used, 0);
            if (kind == TakeReferenceFieldAddress)
            {
                used = Gc(code, used, 0, resultType); // [cell, index] -> traced alias
                return Local(code, used, 0x21, result);
            }
            used = LoadOperand(code, used, value, source, storage);
            return Gc(code, used, 0x0e, vector); // array.set
        }

        private static int Cell(byte[] code, int used, int owner, int receiver, int field)
        {
            used = Local(code, used, 0x20, receiver);
            used = Gc(code, used, 2, owner);
            return WasmBinary.Unsigned(code, used, field);
        }
    }
}
