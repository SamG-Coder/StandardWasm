using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ArrayClearWasm
    {
        internal const int Capacity = 224;

        internal static bool IsVector(int[] objects, int type) => type == NullValue ||
            IsArray(type) || ArrayElement(objects, type) != 0;

        internal static int Emit(byte[] code, int used, int[] objects, int vector, int array, int cursor, int length)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xd1; // ref.is_null
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentNullTag);
            if (vector == NullValue) return used;

            if (length >= 0)
            {
                used = CheckNonNegative(code, used, cursor);
                used = CheckNonNegative(code, used, length);
                used = End(code, used, cursor, length);
                used = Length(code, used, array);
                code[used++] = 0x4b; // i32.gt_u, two nonnegative Int32 operands fit in uint32
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
            }

            int element = ArrayElement(objects, vector);
            if (element != 0 && IsValueType(objects, element))
                return ClearAggregateCells(code, used, vector, array, cursor, length);

            used = Local(code, used, 0x20, array);
            used = length < 0 ? Const(code, used, 0) : Local(code, used, 0x20, cursor);
            used = DefaultElement(code, used, vector, element);
            used = length < 0 ? Length(code, used, array) : Local(code, used, 0x20, length);
            return Gc(code, used, 0x10, vector); // array.fill
        }

        private static int ClearAggregateCells(byte[] code, int used, int vector, int array, int cursor, int length)
        {
            if (length < 0)
            {
                used = Const(code, used, 0);
                used = Local(code, used, 0x21, cursor);
            }
            else used = Local(code, End(code, used, cursor, length), 0x21, length);

            code[used++] = 0x02; code[used++] = 0x40; // block: end of range
            code[used++] = 0x03; code[used++] = 0x40; // loop: inspect next cell
            used = Local(code, used, 0x20, cursor);
            used = length < 0 ? Length(code, used, array) : Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 0x01; // leave block

            used = Cell(code, used, vector, array, cursor);
            code[used++] = 0xd1; code[used++] = 0x45; // a null cell is already default
            code[used++] = 0x04; code[used++] = 0x40;
            used = Cell(code, used, vector, array, cursor);
            code[used++] = 0xd0; code[used++] = 0x6d; // null snapshot
            // Preserve the slot's cell and owner for all existing interior refs.
            used = ManagedValueLocationWasm.WriteSnapshot(code, used);
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, cursor);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, cursor);
            code[used++] = 0x0c; code[used++] = 0x00; // continue loop
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int CheckNonNegative(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
        }

        private static int End(byte[] code, int used, int cursor, int length)
        {
            used = Local(code, used, 0x20, cursor);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x6a;
            return used;
        }

        private static int Length(byte[] code, int used, int array)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            return used;
        }

        private static int Cell(byte[] code, int used, int vector, int array, int cursor) =>
            Gc(code, Local(code, Local(code, used, 0x20, array), 0x20, cursor), 0x0b, vector); // array.get

        private static int DefaultElement(byte[] code, int used, int vector, int element)
        {
            if (element != 0)
            {
                code[used++] = 0xd0;
                code[used++] = NullSpillWasm.NullRef; // bottom reference matches every nullable element heap
                return used;
            }
            if (vector == Longs)
            {
                code[used++] = 0x42; code[used++] = 0;
                return used;
            }
            if (vector == Singles || vector == Doubles)
            {
                code[used++] = vector == Singles ? (byte)0x43 : (byte)0x44;
                int size = vector == Singles ? 4 : 8;
                for (int i = 0; i < size; i++) code[used++] = 0;
                return used;
            }
            return Const(code, used, 0);
        }
    }
}
