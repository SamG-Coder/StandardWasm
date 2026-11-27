using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ExceptionDispatchWasm
    {
        internal const byte ExceptionReference = 0x69; // nullable exnref
        internal const byte ObjectReference = 0x6d;    // nullable eqref: a thrown managed object (decision 0054)

        // Branch depth from an instruction inside the protected body to the
        // method's pc loop: the instruction block, the try_table, one block per
        // fixed tag and the object block.
        internal const int Depth = ExceptionTagsWasm.Count + 3;

        internal static int Begin(byte[] code, int used)
        {
            // The object block is outermost, so its label follows the fixed ones.
            // A plain catch delivers only the (eqref) payload, so the block needs no
            // multi-value type index, and the fixed tags keep their catch_ref paths.
            code[used++] = 0x02;
            code[used++] = ObjectReference;
            // Tag zero is the innermost destination. Each successive tag
            // targets the next enclosing block.
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 0x02;
                code[used++] = ExceptionReference;
            }
            code[used++] = 0x1f; // try_table
            code[used++] = 0x40;
            code[used++] = ExceptionTagsWasm.Count + 1;
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 1; // catch_ref
                code[used++] = (byte)ExceptionTagsWasm.FixedTag(tag);
                code[used++] = (byte)tag;
            }
            code[used++] = 0; // catch
            code[used++] = ExceptionTagsWasm.ObjectTag;
            code[used++] = ExceptionTagsWasm.Count; // the object block
            return used;
        }

        // pc is the pc local. Saved exception locals: exnref at pc + 1 (scratch) and
        // pc + 2 + clause; eqref at objects (scratch) and objects + 1 + clause.
        // Stage 155 (decision 0082): fixedObjects holds, for fixed tags 0 .. FixedObjectCount - 1
        // (Overflow, DivideByZero, IndexOutOfRange, NullReference), CoreLib's parameterless
        // constructor as a module function index (or -1) and its class. Those are the tags the
        // CLR raises with the default message, so a bound handler observes the same object state.
        internal const int FixedObjectCount = 4;
        // Where MethodCompiler passes them in the frame shape, after the initializer failure descriptor.
        internal const int FrameSlot = 23 + TypeInitializerWasm.FailureWidth, FrameWidth = 2 * FixedObjectCount;

        internal static int End(byte[] code, int used, int pc, int[] region, bool saveHandlers, int objects,
            int spills, int maximum, int[] fixedObjects)
        {
            code[used++] = 0x0b; // try_table
            code[used++] = 0;    // only the catch edges may reach the destinations
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 0x0b; // tag destination
                // Remaining fixed blocks plus the object block lie between here and the pc loop.
                int loop = ExceptionTagsWasm.Count - tag;
                used = Dispatch(code, used, pc, region, ExceptionTagsWasm.FixedTag(tag), loop, saveHandlers, objects,
                    spills, maximum, fixedObjects);
            }
            code[used++] = 0x0b; // object destination: [eqref]
            return DispatchObject(code, used, pc, region, saveHandlers, objects, spills, maximum);
        }

        private static int Dispatch(byte[] code, int used, int pc, int[] clauses, int tag, int loop, bool saveHandlers, int objects,
            int spills, int maximum, int[] fixedObjects)
        {
            int captured = pc + 1;
            used = Local(code, used, 0x21, captured);
            // pc still names the throwing instruction. Walk the ordered clauses
            // so an unmatched inner try can fall back to an enclosing one.
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                if (!ExceptionClauses.IsFinally(clauses, clause) &&
                    (clauses[clause + ExceptionClauses.Mask] & (1 << tag)) == 0) continue;
                used = InRange(code, used, pc, clauses, clause);
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                if (ExceptionClauses.IsBound(clauses, clause))
                {
                    used = BindFixed(code, used, pc, clauses, clause, tag, loop, saveHandlers, objects, spills, maximum, fixedObjects);
                    code[used++] = 0x0b;
                    continue;
                }
                if (saveHandlers)
                {
                    int index = clause / ExceptionClauses.Width;
                    used = Local(code, used, 0x20, captured);
                    used = Local(code, used, 0x21, captured + 1 + index);
                    // A fixed exception carries no managed object. Clear any object a
                    // previous visit left, so a rethrow cannot raise a stale payload.
                    used = ClearObject(code, used, objects + 1 + index);
                }
                used = Resume(code, used, pc, clauses, clause, loop);
                code[used++] = 0x0b;
            }
            return ThrowReference(code, used, captured);
        }

        // A fixed tag reaching a bound handler: build the CoreLib exception the CLR would have raised,
        // then enter the handler exactly as for a thrown object. From here on it is that object: a
        // rethrow raises it on the object tag, so an outer handler sees the same reference.
        private static int BindFixed(byte[] code, int used, int pc, int[] clauses, int clause, int tag, int loop,
            bool saveHandlers, int objects, int spills, int maximum, int[] fixedObjects)
        {
            if (tag >= FixedObjectCount || fixedObjects[2 * tag] < 0)
            {
                // InvalidCast and the argument checks carry messages naming types or parameters,
                // which the raising site does not supply: stop explicitly rather than bind an
                // object whose Message would differ from the CLR's.
                code[used++] = 0x00; // unreachable
                return used;
            }
            int heap = WasmBinary.HeapType(fixedObjects[2 * tag + 1]);
            code[used++] = 0xfb; code[used++] = 1; // struct.new_default, as newobj allocates
            used = WasmBinary.Unsigned(code, used, heap);
            used = Local(code, used, 0x22, objects); // local.tee the eqref scratch
            code[used++] = 0xfb; code[used++] = 0x17; // ref.cast (ref null class)
            used = WasmBinary.Signed(code, used, heap);
            code[used++] = 0x10; // call .ctor()
            used = WasmBinary.Unsigned(code, used, fixedObjects[2 * tag]);
            if (saveHandlers)
            {
                int index = clause / ExceptionClauses.Width;
                used = Local(code, used, 0x20, objects);
                used = Local(code, used, 0x21, objects + 1 + index);
                code[used++] = 0xd0; // ref.null exn: a rethrow raises the object
                code[used++] = ExceptionReference;
                used = Local(code, used, 0x21, pc + 2 + index);
            }
            used = StoreCaught(code, used, clauses, clause, objects, spills, maximum);
            return Resume(code, used, pc, clauses, clause, loop);
        }

        // [] -> []: the handler's entry stack slot (depth 0, the caught class) takes the scratch object.
        private static int StoreCaught(byte[] code, int used, int[] clauses, int clause, int objects, int spills, int maximum)
        {
            int caught = ManagedExceptionClauses.CaughtClass(clauses[clause + ExceptionClauses.Mask]);
            used = Local(code, used, 0x20, objects);
            if (caught != PeLibrary.ObjectLayouts.ObjectReference)
            {
                code[used++] = 0xfb; code[used++] = 0x17; // ref.cast (ref null class): already tested or built
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(caught));
            }
            return Local(code, used, 0x21, Spill(spills, maximum, caught, 0));
        }

        private static int DispatchObject(byte[] code, int used, int pc, int[] clauses, bool saveHandlers, int objects,
            int spills, int maximum)
        {
            used = Local(code, used, 0x21, objects);
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                bool cleanup = ExceptionClauses.IsFinally(clauses, clause);
                int mask = clauses[clause + ExceptionClauses.Mask];
                if (!cleanup && !ManagedExceptionClauses.CatchesObjects(mask)) continue;
                used = InRange(code, used, pc, clauses, clause);
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                int caught = cleanup ? PeLibrary.ObjectLayouts.ObjectReference : ManagedExceptionClauses.CaughtClass(mask);
                bool tested = caught != PeLibrary.ObjectLayouts.ObjectReference;
                if (tested)
                {
                    // Nominal match: Wasm GC subtyping mirrors the bundle's class hierarchy, so
                    // a derived exception matches its base and same-named classes stay distinct.
                    used = Local(code, used, 0x20, objects);
                    code[used++] = 0xfb;
                    code[used++] = 20; // ref.test (ref T): a thrown object is never null
                    used = WasmBinary.Signed(code, used, WasmBinary.HeapType(caught));
                    code[used++] = 0x04; // if
                    code[used++] = 0x40;
                }
                if (saveHandlers)
                {
                    int index = clause / ExceptionClauses.Width;
                    used = Local(code, used, 0x20, objects);
                    used = Local(code, used, 0x21, objects + 1 + index);
                    // Clear the fixed exnref a previous visit may have left (and vice versa above).
                    code[used++] = 0xd0; // ref.null exn
                    code[used++] = ExceptionReference;
                    used = Local(code, used, 0x21, pc + 2 + index);
                }
                if (ExceptionClauses.IsBound(clauses, clause))
                    used = StoreCaught(code, used, clauses, clause, objects, spills, maximum);
                // The pc loop lies beyond the clause ifs only: every destination block has closed.
                used = Resume(code, used, pc, clauses, clause, tested ? 1 : 0);
                if (tested) code[used++] = 0x0b;
                code[used++] = 0x0b;
            }
            return ThrowObject(code, used, objects);
        }

        private static int InRange(byte[] code, int used, int pc, int[] clauses, int clause)
        {
            used = Local(code, used, 0x20, pc);
            used = Const(code, used, clauses[clause + ExceptionClauses.TryStart]);
            code[used++] = 0x6b;
            used = Const(code, used, clauses[clause + ExceptionClauses.TryLength]);
            code[used++] = 0x49; // i32.lt_u
            return used;
        }

        // Enter the handler: set pc to its resume offset and branch to the pc loop.
        // extra counts the ifs opened by the caller beyond the clause if.
        private static int Resume(byte[] code, int used, int pc, int[] clauses, int clause, int extra)
        {
            used = Const(code, used, clauses[clause + ExceptionClauses.Resume]);
            used = Local(code, used, 0x21, pc);
            code[used++] = 0x0c;
            code[used++] = (byte)(extra + 1);
            return used;
        }

        private static int ClearObject(byte[] code, int used, int local)
        {
            code[used++] = 0xd0; // ref.null eq
            code[used++] = ObjectReference;
            return Local(code, used, 0x21, local);
        }

        internal static int ThrowReference(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0x0a; // throw_ref
            return used;
        }

        internal static int ThrowObject(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            return ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.ObjectTag);
        }

        // throw; re-raises whichever kind entered the handler: the managed object
        // with its original reference, otherwise the captured fixed exnref.
        internal static int ReraiseCaught(byte[] code, int used, int exception, int obj)
        {
            used = Local(code, used, 0x20, exception);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; // if
            code[used++] = 0x40;
            used = ThrowObject(code, used, obj);
            code[used++] = 0x0b;
            return ThrowReference(code, used, exception);
        }
    }
}
