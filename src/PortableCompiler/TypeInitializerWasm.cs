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
        // Issue #170: when the bundle supplies CoreLib's TypeInitializationException, `failure`
        // describes it (indices below; null keeps the trap). Function and heap indices are final.
        internal const int FailureConstructor = 0, FailureClass = 1, FailureNameStart = 2, FailureNameLength = 3,
            FailureMessageStart = 4, FailureMessageLength = 5, FailureCache = 6, FailureExceptionClass = 7,
            FailureInner = 8, FailureFixed = 9, FailureWidth = FailureFixed + 2 * FixedFailureCount;
        // Step 2: fixed-tag exceptions carry no object. For each fixed tag (in this order) the
        // descriptor holds the CoreLib exception's parameterless constructor (function index, or
        // -1 when unbound) and its class, at FailureFixed + 2 * i and + 1.
        internal const int FixedFailureCount = 7;
        internal static int FixedFailureTag(int i) => i < 6 ? i : 7; // OverflowTag .. ArgumentTag, ArgumentNullTag

        // Overflow, DivideByZero, IndexOutOfRange and NullReference: the CLR raises these with the
        // parameterless constructor's message. InvalidCast and the argument tags carry details.
        internal static bool DefaultMessageMatches(int i) => i < 4;

        private static int BoundFixed(int[] failure)
        {
            int count = 0;
            for (int i = 0; i < FixedFailureCount; i++) if (failure[FailureFixed + 2 * i] >= 0) count++;
            return count;
        }

        internal static int Begin(byte[] code, int used, int guard, int epoch, int active, int scratch, int rootCount, int[] failure)
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
            if (failure != null)
            {
                // A recorded TypeInitializationException is raised again: the same object,
                // with the same inner exception, as the CLR does on every later trigger.
                used = Global(code, used, 0x23, failure[FailureCache]);
                code[used++] = 0xd1; // ref.is_null
                code[used++] = 0x45; // i32.eqz
                code[used++] = 0x04; code[used++] = 0x40;
                used = Global(code, used, 0x23, failure[FailureCache]);
                used = ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.ObjectTag);
                code[used++] = 0x0b;
            }
            if (rootCount >= 0) used = RootStackWasm.ResetAfterInitializerFailure(code, used, rootCount);
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
            if (failure != null)
            {
                // block $fallback (catch_all: an unbound tag still traps)
                //  block $object (result eqref)
                //   block $fixed[k-1] ... block $fixed[0]   one per bound fixed tag, innermost first
                //    try_table (catch tag_i -> i) (catch $objectTag -> k) (catch_all -> k + 1)
                int k = BoundFixed(failure);
                code[used++] = 0x02; code[used++] = 0x6d;
                for (int i = 0; i < k; i++) { code[used++] = 0x02; code[used++] = 0x40; }
                code[used++] = 0x1f; code[used++] = 0x40; // try_table
                used = WasmBinary.Unsigned(code, used, k + 2);
                int label = 0;
                for (int i = 0; i < FixedFailureCount; i++)
                {
                    if (failure[FailureFixed + 2 * i] < 0) continue;
                    code[used++] = 0x00; // catch tag -> its block
                    code[used++] = (byte)FixedFailureTag(i);
                    used = WasmBinary.Unsigned(code, used, label++);
                }
                code[used++] = 0x00; // catch the object tag, carrying the exception object
                code[used++] = ExceptionTagsWasm.ObjectTag;
                used = WasmBinary.Unsigned(code, used, k);
                code[used++] = 0x02; // catch_all
                used = WasmBinary.Unsigned(code, used, k + 1);
                return used;
            }
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
        internal static int End(byte[] code, int used, int guard, int epoch, int active, int rootCount, int[] failure)
        {
            code[used++] = 0x0b; // end try_table
            if (failure != null)
            {
                code[used++] = 0x00; // the body never falls through (its dispatch loop only branches)
                // Each bound fixed tag lands at the end of its block: construct the matching CoreLib
                // exception with its parameterless constructor and deliver it to $object.
                int k = BoundFixed(failure), index = 0;
                for (int i = 0; i < FixedFailureCount; i++)
                {
                    int constructor = failure[FailureFixed + 2 * i];
                    if (constructor < 0) continue;
                    code[used++] = 0x0b; // end block $fixed[index]
                    int heap = WasmBinary.HeapType(failure[FailureFixed + 2 * i + 1]);
                    code[used++] = 0xfb; code[used++] = 1; // struct.new_default
                    used = WasmBinary.Unsigned(code, used, heap);
                    used = BranchWasm.Local(code, used, 0x22, failure[FailureInner]); // local.tee
                    code[used++] = 0xfb; code[used++] = 0x17; // ref.cast (ref null class)
                    used = WasmBinary.Signed(code, used, heap);
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, constructor);
                    used = BranchWasm.Local(code, used, 0x20, failure[FailureInner]);
                    code[used++] = 0x0c; // br $object with the eqref
                    used = WasmBinary.Unsigned(code, used, k - 1 - index);
                    index++;
                }
                code[used++] = 0x0b; // end block (result eqref): the escaped managed object
                used = BranchWasm.Local(code, used, 0x21, failure[FailureInner]);
                // new TypeInitializationException(fullTypeName, message, (Exception)inner), recorded.
                code[used++] = 0xfb; code[used++] = 1; // struct.new_default
                used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(failure[FailureClass]));
                used = Global(code, used, 0x24, failure[FailureCache]);
                used = Global(code, used, 0x23, failure[FailureCache]);
                code[used++] = 0xfb; code[used++] = 0x17; // ref.cast (ref null class)
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(failure[FailureClass]));
                used = Literal(code, used, failure[FailureNameStart], failure[FailureNameLength]);
                used = Literal(code, used, failure[FailureMessageStart], failure[FailureMessageLength]);
                used = BranchWasm.Local(code, used, 0x20, failure[FailureInner]);
                code[used++] = 0xfb; code[used++] = 0x17;
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(failure[FailureExceptionClass]));
                code[used++] = 0x10; // call the constructor
                used = WasmBinary.Unsigned(code, used, failure[FailureConstructor]);
                // A managed exception unwinds frame by frame, so each enclosing initializer
                // catches it in turn: only this guard fails, and no epoch advance is needed.
                used = BranchWasm.Const(code, used, Failed);
                used = Global(code, used, 0x24, guard);
                used = Adjust(code, used, active, -1);
                used = Global(code, used, 0x23, failure[FailureCache]);
                used = ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.ObjectTag);
            }
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
            if (rootCount >= 0) used = RootStackWasm.ResetAfterInitializerFailure(code, used, rootCount);
            code[used++] = 0x00; // unreachable: no TypeInitializationException yet
            return used;
        }

        // A host export entry: (arguments) -> result of `target`. Returns -3 when
        // the destination is too small. Either part is omitted when its globals are -1.
        // The host never re-enters an export while managed frames are live, so at entry
        // no initializer can legitimately be running and no shadow-stack frame is live.
        internal static int ExportEntry(byte[] code, int used, int epoch, int active, int target, int arity,
            int stackPointer = -1, int stackLimit = -1, int pins = -1)
        {
            if (arity > (code.Length - used - ExportCapacity) / 6) return -3;
            code[used++] = 0; // no locals
            if (epoch >= 0)
            {
                used = Global(code, used, 0x23, active);
                code[used++] = 0x04; code[used++] = 0x40; // initializers left running by a raw trap
                used = Adjust(code, used, epoch, 1);
                used = BranchWasm.Const(code, used, 0);
                used = Global(code, used, 0x24, active);
                code[used++] = 0x0b;
            }
            if (stackPointer >= 0)
            {
                // A raw trap can skip a frame's release: start each host call with an
                // empty shadow stack (decision 0065). Zero means not yet reserved.
                used = Global(code, used, 0x23, stackPointer);
                code[used++] = 0x04; code[used++] = 0x40;
                used = Global(code, used, 0x23, stackLimit);
                used = Global(code, used, 0x24, stackPointer);
                code[used++] = 0x0b;
            }
            if (pins >= 0) used = PinRegistryWasm.Reset(code, used, pins);
            for (int i = 0; i < arity; i++) used = BranchWasm.Local(code, used, 0x20, i);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, target);
            code[used++] = 0x0b;
            return used;
        }

        private const int ExportCapacity = 64;

        private static int Adjust(byte[] code, int used, int global, int delta)
        {
            used = Global(code, used, 0x23, global);
            used = BranchWasm.Const(code, used, delta);
            code[used++] = 0x6a; // i32.add
            return Global(code, used, 0x24, global);
        }

        internal static int CallInitializer(byte[] code, int used, int function)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, function);
        }

        internal static bool HasFieldTrigger(int kind, int global, int[] fieldInitializers) =>
            (kind == BranchIl.LoadStaticField || kind == BranchIl.StoreStaticField ||
             kind == BranchIl.StaticFieldAddress || kind == BranchIl.LoadNativeStatic || kind == BranchIl.StoreNativeStatic ||
             kind == BranchIl.StaticInt32 || kind == BranchIl.StaticReference || kind == BranchIl.StaticValue) &&
            global >= 0 && global < fieldInitializers.Length && fieldInitializers[global] != 0;

        // A static field access of a type with an initializer runs it first.
        internal static int Trigger(byte[] code, int used, int[] fieldInitializers, int global, int hostImports)
        {
            if (global >= fieldInitializers.Length || fieldInitializers[global] == 0) return used;
            return CallInitializer(code, used, hostImports + fieldInitializers[global] - 1);
        }

        internal const int Capacity = 96, TriggerCapacity = 6;

        // A String from the module literal pool: units [start, start + 2 * length) of data segment 0.
        private static int Literal(byte[] code, int used, int start, int length)
        {
            used = BranchWasm.Const(code, used, start);
            used = BranchWasm.Const(code, used, length);
            code[used++] = 0xfb; code[used++] = 9; code[used++] = 4; code[used++] = 0; // array.new_data Char[]
            return StringWasm.Wrap(code, used);
        }

        private static int Global(byte[] code, int used, byte instruction, int index)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
