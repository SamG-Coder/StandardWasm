namespace PortableCompiler
{
    // Stage 129 (decision 0057). Guard state of one type initializer, in one i32 global:
    //   0                  not started
    //   (epoch << 2) | 1   running, started during failure epoch `epoch`
    //   2                  completed
    //   3                  failed
    // A managed exception escaping an initializer marks it failed, advances the
    // module's failure epoch and traps. The trap unwinds every frame, so any
    // initializer still marked running from an older epoch was interrupted by that
    // failure and is treated as failed too. A running state from the current epoch
    // is a legitimate recursive trigger on the same thread and returns at once, as
    // on the CLR. TypeInitializationException is not constructed yet: managed code
    // never observes the failure, and a later trigger on the same instance traps again.
    //
    // A raw engine trap bypasses catch_all and leaves running guards in the current
    // epoch. A second global counts initializers on the Wasm stack. Each host export
    // entry that finds it nonzero advances the epoch, so those interrupted guards
    // read as failed rather than as recursion. This assumes the host never re-enters
    // an export while an initializer is running (single managed thread, decision 0026).
    internal static class TypeInitializerWasm
    {
        internal const int Running = 1, Completed = 2, Failed = 3;

        // Emitted before the body's dispatch loop. `scratch` is the pc local, which
        // is reset to the entry offset afterwards. Leaves `block; try_table` open.
        internal static int Begin(byte[] code, int used, int guard, int epoch, int active, int scratch)
        {
            used = Global(code, used, 0x23, guard);
            used = BranchWasm.Local(code, used, 0x21, scratch);
            used = BranchWasm.Local(code, used, 0x20, scratch);
            code[used++] = 0x04; code[used++] = 0x40; // if started
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = BranchWasm.Const(code, used, Completed);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f; // completed: return
            code[used++] = 0x0b;
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = BranchWasm.Const(code, used, 3);
            code[used++] = 0x71; // i32.and
            used = BranchWasm.Const(code, used, Running);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40; // if running
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = BranchWasm.Const(code, used, 2);
            code[used++] = 0x76; // i32.shr_u
            used = Global(code, used, 0x23, epoch);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f; // running in this epoch: recursive trigger returns
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            code[used++] = 0x00; // failed, or interrupted by an earlier failure: trap
            code[used++] = 0x0b;

            used = Global(code, used, 0x23, epoch);
            used = BranchWasm.Const(code, used, 2);
            code[used++] = 0x74; // i32.shl
            used = BranchWasm.Const(code, used, Running);
            code[used++] = 0x72; // i32.or
            used = Global(code, used, 0x24, guard);
            used = Adjust(code, used, active, 1);
            used = BranchWasm.Const(code, used, 0);
            used = BranchWasm.Local(code, used, 0x21, scratch);

            code[used++] = 0x02; code[used++] = 0x40; // block: managed failure lands here
            code[used++] = 0x1f; code[used++] = 0x40; // try_table
            code[used++] = 1;
            code[used++] = 0x02; // catch_all: every managed tag, fixed or object
            code[used++] = 0;
            return used;
        }

        // Emitted before each IL ret of the initializer body.
        internal static int Complete(byte[] code, int used, int guard, int active)
        {
            used = BranchWasm.Const(code, used, Completed);
            used = Global(code, used, 0x24, guard);
            return Adjust(code, used, active, -1);
        }

        // Closes Begin's try_table and block after the (unreachable) loop exit.
        internal static int End(byte[] code, int used, int guard, int epoch, int active)
        {
            code[used++] = 0x0b; // end try_table
            code[used++] = 0x0b; // end block
            used = BranchWasm.Const(code, used, Failed);
            used = Global(code, used, 0x24, guard);
            used = Global(code, used, 0x23, epoch);
            used = BranchWasm.Const(code, used, 1);
            code[used++] = 0x6a; // i32.add
            used = Global(code, used, 0x24, epoch);
            // The trap unwinds every running initializer; the new epoch covers them.
            used = BranchWasm.Const(code, used, 0);
            used = Global(code, used, 0x24, active);
            code[used++] = 0x00; // unreachable: no TypeInitializationException yet
            return used;
        }

        // A host export entry: (arguments) -> result of `target`. Returns -3 when
        // the destination is too small.
        internal static int ExportEntry(byte[] code, int used, int epoch, int active, int target, int arity)
        {
            if (arity > (code.Length - used - ExportCapacity) / 6) return -3;
            code[used++] = 0; // no locals
            used = Global(code, used, 0x23, active);
            code[used++] = 0x04; code[used++] = 0x40; // initializers left running by a raw trap
            used = Adjust(code, used, epoch, 1);
            used = BranchWasm.Const(code, used, 0);
            used = Global(code, used, 0x24, active);
            code[used++] = 0x0b;
            for (int i = 0; i < arity; i++) used = BranchWasm.Local(code, used, 0x20, i);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, target);
            code[used++] = 0x0b;
            return used;
        }

        private const int ExportCapacity = 48;

        private static int Adjust(byte[] code, int used, int global, int delta)
        {
            used = Global(code, used, 0x23, global);
            used = BranchWasm.Const(code, used, delta);
            code[used++] = 0x6a; // i32.add
            return Global(code, used, 0x24, global);
        }

        // A static field access of a type with an initializer runs it first.
        internal static int Trigger(byte[] code, int used, int[] fieldInitializers, int global, int hostImports)
        {
            if (global >= fieldInitializers.Length || fieldInitializers[global] == 0) return used;
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, hostImports + fieldInitializers[global] - 1);
        }

        internal const int Capacity = 96, TriggerCapacity = 6;

        private static int Global(byte[] code, int used, byte instruction, int index)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
