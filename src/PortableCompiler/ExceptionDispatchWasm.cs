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
        internal static int End(byte[] code, int used, int pc, int[] region, bool saveHandlers, int objects, int[] filters)
        {
            code[used++] = 0x0b; // try_table
            code[used++] = 0;    // only the catch edges may reach the destinations
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 0x0b; // tag destination
                // Remaining fixed blocks plus the object block lie between here and the pc loop.
                int loop = ExceptionTagsWasm.Count - tag;
                used = Dispatch(code, used, pc, region, ExceptionTagsWasm.FixedTag(tag), loop, saveHandlers, objects);
            }
            code[used++] = 0x0b; // object destination: [eqref]
            return DispatchObject(code, used, pc, region, saveHandlers, objects, filters);
        }

        private static int Dispatch(byte[] code, int used, int pc, int[] clauses, int tag, int loop, bool saveHandlers, int objects)
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

        private static int DispatchObject(byte[] code, int used, int pc, int[] clauses, bool saveHandlers, int objects, int[] filters)
        {
            used = Local(code, used, 0x21, objects);
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                bool cleanup = ExceptionClauses.IsFinally(clauses, clause);
                int mask = clauses[clause + ExceptionClauses.Mask];
                int filter = filters.Length == 0 ? 0 : filters[clause / ExceptionClauses.Width * ExceptionFilters.Width];
                bool firstObject = ManagedExceptionClauses.CatchesObjects(mask);
                bool secondObject = ManagedExceptionClauses.CatchesObjects(filter);
                if (!cleanup && !firstObject && !secondObject) continue;
                used = InRange(code, used, pc, clauses, clause);
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                int caught = cleanup ? PeLibrary.ObjectLayouts.ObjectReference : ManagedExceptionClauses.CaughtClass(firstObject ? mask : filter);
                int other = firstObject && secondObject ? ManagedExceptionClauses.CaughtClass(filter) : 0;
                bool tested = caught != PeLibrary.ObjectLayouts.ObjectReference && other != PeLibrary.ObjectLayouts.ObjectReference;
                if (tested)
                {
                    // Nominal match: Wasm GC subtyping mirrors the bundle's class hierarchy, so
                    // a derived exception matches its base and same-named classes stay distinct.
                    used = IsInstance(code, used, objects, caught);
                    if (other != 0 && other != caught)
                    {
                        used = IsInstance(code, used, objects, other);
                        code[used++] = 0x72; // i32.or
                    }
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
                // The pc loop lies beyond the clause ifs only: every destination block has closed.
                used = Resume(code, used, pc, clauses, clause, tested ? 1 : 0);
                if (tested) code[used++] = 0x0b;
                code[used++] = 0x0b;
            }
            return ThrowObject(code, used, objects);
        }

        private static int IsInstance(byte[] code, int used, int objects, int type)
        {
            used = Local(code, used, 0x20, objects);
            code[used++] = 0xfb;
            code[used++] = 20; // ref.test (ref T)
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
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
