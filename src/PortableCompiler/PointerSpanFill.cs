using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Decision 0105: `new Span<T>(void* pointer, int length).Fill(constant)` and `.Clear()` for a
    // primitive T, in the shape C# emits:
    //   <pointer>; <length>; newobj Span<T>::.ctor(void*, int32); stloc S; ldloca S; [ldc V;] call Fill/Clear
    // The span never escapes S, so the sequence is one native fill: [pointer, length(, V)] -> [].
    //   length < 0 -> ArgumentOutOfRangeException (the constructor's check)
    //   pointer == 0 with length != 0 -> trap (the CLR's access violation)
    // A value whose bytes are all equal (0, -1, 0xFF..) is a single memory.fill; any other value
    // stores each element. S must have no other use; it stays an unused local.
    internal static class PointerSpanFill
    {
        internal const int Capacity = 160, Cleared = 0x100;

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            for (int create = 0; create < records.Length; create += Width)
            {
                if (records[create] != MemberCall || records[create + 3] != 0x73) continue;
                if (PinnedArrays.PointerSpanMember(image, layout, heaps, records[create + 1]) != 1) continue;
                int lane = PinnedArrays.PointerSpanLane(image, layout, heaps, records[create + 1]);
                int store = Next(records, create, clauses), address = Next(records, store, clauses);
                if (address < 0 || records[store] != Store || records[address] != FrameAddress ||
                    records[address + 1] != records[store + 1]) continue;
                int local = records[store + 1];
                int value = Next(records, address, clauses);
                if (value < 0) continue;
                bool clear = records[value] == MemberCall &&
                    PinnedArrays.PointerSpanMember(image, layout, heaps, records[value + 1]) == 3;
                int call = clear ? value : Next(records, value, clauses);
                if (call < 0 || records[call] != MemberCall || records[call + 3] != 0x28) continue;
                if (!clear && (PinnedArrays.PointerSpanMember(image, layout, heaps, records[call + 1]) != 2 ||
                    !IsLaneConstant(records, value, lane))) continue;
                if (!SoleUse(records, local, store, address)) continue;
                records[create] = Nop;
                records[store] = Nop;
                records[address] = Nop;
                records[call] = NativeSpanFill;
                records[call + 1] = lane | (clear ? Cleared : 0);
                // aux = the memory.fill byte + 1 when the value's bytes are uniform, else 0.
                records[call + 3] = clear ? 1 : Uniform(lane, records[value + 1], records[value + 3]) + 1;
            }
        }

        private static int Next(int[] records, int slot, int[] clauses)
        {
            if (slot < 0) return -1;
            int next = records[slot + 2] * Width;
            return next < records.Length && !ValueOperands.Entry(records, next / Width, clauses) ? next : -1;
        }

        // The fill value is a constant of the lane's stack type.
        private static bool IsLaneConstant(int[] records, int value, int lane) =>
            lane == 0x0a || lane == 0x0b ? records[value] == WideConstant :
            lane == 0x0c ? records[value] == SingleConstant : lane == 0x0d ? records[value] == DoubleConstant :
            records[value] == Constant;

        private static bool SoleUse(int[] records, int local, int store, int address)
        {
            for (int at = 0; at < records.Length; at += Width)
            {
                if (at == store || at == address) continue;
                int kind = records[at];
                if ((kind == Load || kind == Store || kind == FrameAddress) && records[at + 1] == local) return false;
            }
            return true;
        }

        internal static int Arity(int operand) => (operand & Cleared) != 0 ? 2 : 3;

        internal static int Size(int operand)
        {
            int lane = operand & 0xff;
            return lane == 0x04 || lane == 0x05 || lane == 0x02 ? 1 : lane == 0x06 || lane == 0x07 || lane == 0x03 ? 2 :
                lane == 0x0a || lane == 0x0b || lane == 0x0d ? 8 : 4;
        }

        internal static int ValueType(int operand)
        {
            int lane = operand & 0xff;
            return lane == 0x0a || lane == 0x0b ? SignatureTypes.I64 : lane == 0x0c ? SignatureTypes.F32 :
                lane == 0x0d ? SignatureTypes.F64 : SignatureTypes.I32;
        }

        // [pointer, length(, value)] -> []. `index` is an i32 scratch local. `uniform` is the fill
        // byte when the constant value's bytes are all equal, otherwise -1.
        internal static int Emit(byte[] code, int used, int operand, int pointer, int length, int value, int index, int uniform)
        {
            int size = Size(operand);
            used = Local(code, used, 0x20, length);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            // pointer == 0 && length != 0 -> trap
            used = Local(code, used, 0x20, pointer);
            code[used++] = 0x45;
            used = Local(code, used, 0x20, length);
            code[used++] = 0x45; code[used++] = 0x45;
            code[used++] = 0x71;
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b; // if: unreachable
            if ((operand & Cleared) != 0) uniform = 0;
            if (uniform >= 0)
            {
                used = Local(code, used, 0x20, pointer);
                used = Const(code, used, uniform);
                used = Local(code, used, 0x20, length);
                if (size != 1)
                {
                    used = Const(code, used, size);
                    code[used++] = 0x6c; // i32.mul: a 2 GB span of a wider lane still fits 32 bits here
                }
                code[used++] = 0xfc; code[used++] = 11; code[used++] = 0; // memory.fill
                return used;
            }
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1;
            used = Local(code, used, 0x20, pointer);
            used = Local(code, used, 0x20, index);
            if (size != 1)
            {
                used = Const(code, used, size);
                code[used++] = 0x6c;
            }
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, value);
            int lane = operand & 0xff;
            code[used++] = size == 1 ? (byte)0x3a : size == 2 ? (byte)0x3b : lane == 0x0c ? (byte)0x38 :
                lane == 0x0d ? (byte)0x39 : size == 8 ? (byte)0x37 : (byte)0x36;
            code[used++] = 0; code[used++] = 0;
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        // The fill byte when every byte of the lane-sized constant is the same, otherwise -1.
        internal static int Uniform(int operand, int low, int high)
        {
            int size = Size(operand);
            if ((operand & 0xff) == 0x0c || (operand & 0xff) == 0x0d) return -1; // floats keep the store loop
            int first = low & 0xff;
            for (int i = 1; i < size; i++)
            {
                int word = i < 4 ? low : high;
                if (((word >> (8 * (i & 3))) & 0xff) != first) return -1;
            }
            return first;
        }
    }
}
