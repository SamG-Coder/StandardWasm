using static PortableCompiler.BranchWasm;
using static PortableCompiler.WasmBinary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal static class EventWasm
    {
        // Stack on entry: target, typed thunk. Wrap the callback in a one-entry list.
        internal static int NewList(byte[] code, int used)
        {
            used = Instruction(code, used, 0, EventEntryHeap); // struct.new
            used = Instruction(code, used, 8, EventHandlerHeap); // array.new_fixed
            code[used++] = 1;
            return used;
        }

        internal static int CombineLists(byte[] code, int used, int left, int right, int scratch)
        {
            int leftLength = scratch, rightLength = scratch + 1, output = scratch + 2;
            // Either null operand returns the other reference. Only the nonnull
            // pair allocates, leaving operand invocation lists intact.
            used = IfNull(code, used, left);
            used = Local(code, used, 0x20, right);
            code[used++] = 0x05;
            used = IfNull(code, used, right);
            used = Local(code, used, 0x20, left);
            code[used++] = 0x05;
            used = Length(code, used, left);
            used = Local(code, used, 0x22, leftLength);
            used = Length(code, used, right);
            used = Local(code, used, 0x22, rightLength);
            code[used++] = 0x6a; // i32.add
            used = Instruction(code, used, 7, EventHandlerHeap);
            used = Local(code, used, 0x21, output);
            used = Copy(code, used, output, -1, left, leftLength);
            used = Copy(code, used, output, leftLength, right, rightLength);
            used = Local(code, used, 0x20, output);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        internal static int Invoke(byte[] code, int used, int list, int sender, int arguments, int scratch)
        {
            int index = scratch, length = scratch + 1;
            // The caller's spill holds the snapshot for the entire invocation.
            // array.len traps for a null delegate before any callback executes.
            used = Length(code, used, list);
            used = Local(code, used, 0x21, length);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; // block for loop exit
            code[used++] = 0x40;
            code[used++] = 0x03; // loop
            code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; // br_if exits the containing block
            code[used++] = 1;
            used = EntryField(code, used, list, index, 0);
            used = Local(code, used, 0x20, sender);
            used = Local(code, used, 0x20, arguments);
            used = EntryField(code, used, list, index, 1);
            code[used++] = 0x14; // call_ref
            code[used++] = EventThunkType;
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; // br to loop header
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int IfNull(byte[] code, int used, int reference)
        {
            used = Local(code, used, 0x20, reference);
            code[used++] = 0xd1;
            code[used++] = 0x04;
            return ValueType(code, used, EventHandlerReference);
        }

        private static int Length(byte[] code, int used, int array)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb;
            code[used++] = 15;
            return used;
        }

        private static int Copy(byte[] code, int used, int output, int offset, int array, int length)
        {
            used = Local(code, used, 0x20, output);
            used = offset < 0 ? Const(code, used, 0) : Local(code, used, 0x20, offset);
            used = Local(code, used, 0x20, array);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, length);
            used = Instruction(code, used, 17, EventHandlerHeap);
            code[used++] = EventHandlerHeap;
            return used;
        }

        private static int EntryField(byte[] code, int used, int list, int index, byte field)
        {
            used = Local(code, used, 0x20, list);
            used = Local(code, used, 0x20, index);
            used = Instruction(code, used, 11, EventHandlerHeap); // array.get
            used = Instruction(code, used, 2, EventEntryHeap); // struct.get
            code[used++] = field;
            return used;
        }

        private static int Instruction(byte[] code, int used, byte opcode, byte heap)
        {
            code[used++] = 0xfb;
            code[used++] = opcode;
            code[used++] = heap;
            return used;
        }
    }
}
