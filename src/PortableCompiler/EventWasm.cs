using static PortableCompiler.BranchWasm;
using static PortableCompiler.WasmBinary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal static class EventWasm
    {
        // Entry fields (decision 0092): target, method (ref func), method identity, nominal delegate
        // type, singleton list.
        internal const byte EntryTarget = 0, EntryMethod = 1, EntryIdentity = 2, EntryDelegateType = 3, EntrySingleton = 4;

        // Stack on entry: target, thunk. Wrap the callback in a one-entry list of this nominal type.
        internal static int NewList(byte[] code, int used, int method, int singleton, int delegateType)
        {
            used = Local(code, used, 0x20, method);
            used = Const(code, used, delegateType);
            used = NullList(code, used);
            used = Instruction(code, used, 0, EventEntryHeap); // struct.new
            used = Instruction(code, used, 8, EventHandlerHeap); // array.new_fixed
            code[used++] = 1;
            used = Local(code, used, 0x22, singleton);
            used = Const(code, used, 0);
            used = Instruction(code, used, 11, EventHandlerHeap);
            used = Local(code, used, 0x20, singleton);
            used = Instruction(code, used, 5, EventEntryHeap); // struct.set singleton
            code[used++] = EntrySingleton;
            used = Local(code, used, 0x20, singleton);
            return used;
        }

        internal static int CombineLists(byte[] code, int used, int left, int right, int scratch)
        {
            int leftLength = scratch, rightLength = scratch + 1, output = scratch + 5;
            // Either null operand returns the other reference. Only the nonnull
            // pair allocates, leaving operand invocation lists intact.
            used = IfNull(code, used, left);
            used = Local(code, used, 0x20, right);
            code[used++] = 0x05;
            used = IfNull(code, used, right);
            used = Local(code, used, 0x20, left);
            code[used++] = 0x05;
            used = RejectMismatch(code, used, left, right);
            used = Length(code, used, left);
            used = Local(code, used, 0x22, leftLength);
            used = Length(code, used, right);
            used = Local(code, used, 0x22, rightLength);
            code[used++] = 0x6a; // i32.add
            used = Instruction(code, used, 7, EventHandlerHeap);
            used = Local(code, used, 0x21, output);
            used = Copy(code, used, output, -1, left, -1, leftLength);
            used = Copy(code, used, output, leftLength, right, -1, rightLength);
            used = Local(code, used, 0x20, output);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        internal static int RemoveLast(byte[] code, int used, int source, int value, int scratch)
        {
            int start = scratch, valueLength = scratch + 1, remaining = scratch + 4;
            used = IfNull(code, used, source);
            used = Local(code, used, 0x20, source);
            code[used++] = 0x05;
            used = IfNull(code, used, value);
            used = Local(code, used, 0x20, source);
            code[used++] = 0x05;
            used = RejectMismatch(code, used, source, value);
            used = Length(code, used, source);
            used = Length(code, used, value);
            used = Local(code, used, 0x22, valueLength);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, remaining);
            used = Local(code, used, 0x21, start);
            used = FindLast(code, used, source, value, scratch);
            used = Local(code, used, 0x20, start);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            used = IfList(code, used);
            used = Local(code, used, 0x20, source);
            code[used++] = 0x05;
            used = RemoveMatch(code, used, source, scratch);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int FindLast(byte[] code, int used, int source, int value, int scratch)
        {
            int start = scratch, length = scratch + 1, item = scratch + 2, sourceItem = scratch + 3;
            // Search backwards by candidate start. Each candidate is compared
            // forwards using both the bound receiver and the method identity.
            code[used++] = 0x02; // search exit
            code[used++] = 0x40;
            code[used++] = 0x03; // candidate loop
            code[used++] = 0x40;
            used = Local(code, used, 0x20, start);
            used = Const(code, used, 0);
            code[used++] = 0x48;
            code[used++] = 0x0d;
            code[used++] = 1; // exhausted candidates
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, item);
            code[used++] = 0x02; // mismatch continues at the next candidate
            code[used++] = 0x40;
            code[used++] = 0x03; // item loop
            code[used++] = 0x40;
            used = Local(code, used, 0x20, item);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46;
            code[used++] = 0x0d;
            code[used++] = 3; // found an entire match, exit the search
            used = Local(code, used, 0x20, start);
            used = Local(code, used, 0x20, item);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, sourceItem);
            used = EntryField(code, used, source, sourceItem, 0);
            used = EntryField(code, used, value, item, 0);
            code[used++] = 0xd3; // ref.eq
            code[used++] = 0x45;
            code[used++] = 0x0d;
            code[used++] = 1;
            used = EntryField(code, used, source, sourceItem, 2);
            used = EntryField(code, used, value, item, 2);
            code[used++] = 0x47; // i32.ne
            code[used++] = 0x0d;
            code[used++] = 1;
            used = Local(code, used, 0x20, item);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, item);
            code[used++] = 0x0c;
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, start);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x21, start);
            code[used++] = 0x0c;
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int RemoveMatch(byte[] code, int used, int source, int scratch)
        {
            int start = scratch, valueLength = scratch + 1, skip = scratch + 2, tail = scratch + 3;
            int remaining = scratch + 4, output = scratch + 5;
            used = Local(code, used, 0x20, remaining);
            code[used++] = 0x45;
            used = IfList(code, used);
            used = NullList(code, used);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, remaining);
            used = Const(code, used, 1);
            code[used++] = 0x46;
            used = IfList(code, used);
            // A sole survivor returns its original delegate, whether it was
            // before or after the removed subsequence.
            used = Local(code, used, 0x20, valueLength);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, start);
            code[used++] = 0x45;
            code[used++] = 0x1b;
            used = Local(code, used, 0x21, skip);
            used = EntryField(code, used, source, skip, EntrySingleton);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, remaining);
            used = Instruction(code, used, 7, EventHandlerHeap);
            used = Local(code, used, 0x21, output);
            used = Copy(code, used, output, -1, source, -1, start);
            used = Local(code, used, 0x20, start);
            used = Local(code, used, 0x20, valueLength);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, skip);
            used = Local(code, used, 0x20, remaining);
            used = Local(code, used, 0x20, start);
            code[used++] = 0x6b;
            used = Local(code, used, 0x21, tail);
            used = Copy(code, used, output, start, source, skip, tail);
            used = Local(code, used, 0x20, output);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        internal static int Invoke(byte[] code, int used, int list, int sender, int arguments, int scratch)
        {
            int index = scratch, length = scratch + 1;
            // The caller's spill holds the snapshot for the entire invocation.
            // The caller checks null after evaluating the event arguments.
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
            used = EntryField(code, used, list, index, EntryMethod);
            code[used++] = 0xfb; // ref.cast (ref EventThunkType): the entry holds any function
            code[used++] = 0x16;
            code[used++] = EventThunkType;
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

        // Decision 0092: Combine or Remove of two nonnull delegates of different nominal types raises
        // ArgumentException, as on the CLR. Every entry of a list shares its nominal type.
        private static int RejectMismatch(byte[] code, int used, int left, int right)
        {
            used = EntryField(code, used, left, -1, EntryDelegateType);
            used = EntryField(code, used, right, -1, EntryDelegateType);
            code[used++] = 0x47; // i32.ne
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
        }

        private static int IfNull(byte[] code, int used, int reference)
        {
            used = Local(code, used, 0x20, reference);
            code[used++] = 0xd1;
            return IfList(code, used);
        }

        private static int IfList(byte[] code, int used)
        {
            code[used++] = 0x04;
            return ValueType(code, used, EventHandlerReference);
        }

        private static int NullList(byte[] code, int used)
        {
            code[used++] = 0xd0;
            code[used++] = EventHandlerHeap;
            return used;
        }

        private static int Length(byte[] code, int used, int array)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb;
            code[used++] = 15;
            return used;
        }

        // A negative offset selects index zero instead of a local.
        private static int Copy(byte[] code, int used, int output, int offset, int array, int sourceOffset, int length)
        {
            used = Local(code, used, 0x20, output);
            used = offset < 0 ? Const(code, used, 0) : Local(code, used, 0x20, offset);
            used = Local(code, used, 0x20, array);
            used = sourceOffset < 0 ? Const(code, used, 0) : Local(code, used, 0x20, sourceOffset);
            used = Local(code, used, 0x20, length);
            used = Instruction(code, used, 17, EventHandlerHeap);
            code[used++] = EventHandlerHeap;
            return used;
        }

        private static int EntryField(byte[] code, int used, int list, int index, byte field)
        {
            used = Local(code, used, 0x20, list);
            used = index < 0 ? Const(code, used, 0) : Local(code, used, 0x20, index); // negative: entry 0
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
