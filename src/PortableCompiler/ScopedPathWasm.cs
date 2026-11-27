using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ScopedPathWasm
    {
        internal const int Capacity = 1380;

        internal static int WriteBody(byte[] code, int used, int import, int errorBase,
            int initializer, int hostImports, int[] stack, bool withFilePart)
        {
            const int Input = 0, FilePart = 3;
            int OutputCapacity = withFilePart ? 1 : 2, Output = withFilePart ? 2 : 1;
            int InputArray = withFilePart ? 4 : 3, OutputArray = InputArray + 1,
                InputAddress = InputArray + 2, OutputAddress = InputArray + 3,
                InputStart = InputArray + 4, OutputStart = InputArray + 5, InputUnits = InputArray + 6,
                OutputUnits = InputArray + 7, Index = InputArray + 8, Size = InputArray + 9,
                Scratch = InputArray + 10, Saved = InputArray + 11, Result = InputArray + 12, SystemError = InputArray + 13;
            code[used++] = 2; // nullable packed char arrays, then i32 locals
            code[used++] = 2;
            code[used++] = 0x63;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(Chars));
            code[used++] = (byte)(SystemError - InputAddress + 1);
            code[used++] = 0x7f;
            if (initializer >= 0) used = Call(code, used, hostImports + initializer);

            used = Decode(code, used, Input, InputArray, InputAddress, InputStart, InputUnits, errorBase);
            used = Decode(code, used, Output, OutputArray, OutputAddress, OutputStart, OutputUnits, errorBase);
            used = IfArray(code, used, OutputArray);
            used = Local(code, used, 0x20, OutputCapacity);
            used = Local(code, used, 0x20, OutputUnits);
            code[used++] = 0x4b; // capacity > available
            used = FailIf(code, used, 998, errorBase);
            // A native file-part pointer may outlive this call. Only a real
            // linear output can publish one. A sizing query writes no output.
            if (withFilePart)
            {
                used = Local(code, used, 0x20, FilePart);
                used = NonZero(code, used);
                used = Local(code, used, 0x20, OutputCapacity);
                used = NonZero(code, used);
                code[used++] = 0x71;
                used = FailIf(code, used, 50, errorBase);
            }
            used = Local(code, used, 0x20, OutputCapacity);
            used = Const(code, used, 32768);
            code[used++] = 0x4b;
            used = FailIf(code, used, 50, errorBase);
            code[used++] = 0x0b;

            int sp = stack[0] - 1, stackBase = stack[1] - 1, limit = stack[2] - 1;
            used = StackAllocationWasm.Begin(code, used, sp, Saved);
            used = IfArray(code, used, InputArray);
            // Copy a bounded suffix and a trailing zero. The CLR guarantees
            // this terminator for string-backed views, outside String.Length.
            // The host sees at most the maximum native path plus one unit.
            used = Const(code, used, 32767);
            used = Local(code, used, 0x20, InputUnits);
            used = Local(code, used, 0x20, InputUnits);
            used = Const(code, used, 32767);
            code[used++] = 0x4b;
            code[used++] = 0x1b; // min(input units, 32767)
            used = Local(code, used, 0x22, InputUnits);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = ToByteSize(code, used, Size);
            used = StackAllocationWasm.Allocate(code, used, sp, stackBase, limit, Size, InputAddress, Scratch);
            used = ScopedCharacterPinWasm.Copy(code, used, InputArray, InputStart, InputUnits, InputAddress, Index, false);
            code[used++] = 0x0b;
            used = IfArray(code, used, OutputArray);
            used = Local(code, used, 0x20, OutputCapacity);
            used = ToByteSize(code, used, Size);
            used = StackAllocationWasm.Allocate(code, used, sp, stackBase, limit, Size, OutputAddress, Scratch);
            // Keep original bytes when a native error leaves the buffer unchanged.
            used = ScopedCharacterPinWasm.Copy(code, used, OutputArray, OutputStart, OutputCapacity, OutputAddress, Index, false);
            code[used++] = 0x0b;

            used = Const(code, used, 0);
            used = Call(code, used, errorBase + MarshalErrorMetadata.SetSystem - 1);
            used = Local(code, used, 0x20, InputAddress);
            used = Local(code, used, 0x20, withFilePart ? OutputCapacity : OutputAddress);
            used = Local(code, used, 0x20, withFilePart ? OutputAddress : OutputCapacity);
            if (withFilePart) used = Local(code, used, 0x20, FilePart);
            used = Call(code, used, import);
            used = Local(code, used, 0x21, Result);
            used = Call(code, used, errorBase + MarshalErrorMetadata.GetSystem - 1);
            used = Local(code, used, 0x21, SystemError);
            used = IfArray(code, used, OutputArray);
            used = ScopedCharacterPinWasm.Copy(code, used, OutputArray, OutputStart, OutputCapacity, OutputAddress, Index, true);
            code[used++] = 0x0b;
            used = StackAllocationWasm.Release(code, used, sp, limit, Saved);
            used = Local(code, used, 0x20, SystemError);
            used = Call(code, used, errorBase + MarshalErrorMetadata.SetPInvoke - 1);
            used = Local(code, used, 0x20, Result);
            code[used++] = 0x0b;
            return used;
        }

        private static int Decode(byte[] code, int used, int location, int array, int address, int start, int available, int errors)
        {
            used = Local(code, used, 0x20, location);
            code[used++] = 0xd1;
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = ManagedLocationWasm.LoadField(code, used, location, 0);
            code[used++] = 0xfb; code[used++] = 0x17; // ref.cast null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(Chars));
            used = Local(code, used, 0x21, array);
            used = ManagedLocationWasm.LoadField(code, used, location, 1);
            used = Local(code, used, 0x21, address);
            used = IfArray(code, used, array);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, available);
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x22, start);
            code[used++] = 0x49; // length < offset, including negative offsets
            used = FailIf(code, used, 998, errors);
            used = Local(code, used, 0x20, available);
            used = Local(code, used, 0x20, start);
            code[used++] = 0x6b;
            used = Local(code, used, 0x21, available);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int ToByteSize(byte[] code, int used, int size)
        {
            used = Const(code, used, 1);
            code[used++] = 0x74;
            return Local(code, used, 0x21, size);
        }

        private static int FailIf(byte[] code, int used, int error, int errorBase)
        {
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, error);
            used = Call(code, used, errorBase + MarshalErrorMetadata.SetSystem - 1);
            used = Const(code, used, error);
            used = Call(code, used, errorBase + MarshalErrorMetadata.SetPInvoke - 1);
            used = Const(code, used, 0);
            code[used++] = 0x0f; // return
            code[used++] = 0x0b;
            return used;
        }

        private static int IfArray(byte[] code, int used, int array)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xd1;
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            return used;
        }

        private static int NonZero(byte[] code, int used)
        {
            code[used++] = 0x45; code[used++] = 0x45;
            return used;
        }

        private static int Call(byte[] code, int used, int index)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
