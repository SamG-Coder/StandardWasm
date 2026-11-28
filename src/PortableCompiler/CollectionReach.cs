using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // Decision 0106: which compiled methods can reach a collection.
    //
    // With finalizer tracking (decision 0104), a collection starts only inside GC.Collect or
    // GC.WaitForPendingFinalizers, and it reads exactly the roots that the frames on the stack
    // published at their pending call sites. A frame's references therefore need publishing only
    // around calls that can (transitively) reach one of those members. A method may collect when
    // it calls GC.Collect or WaitForPendingFinalizers, makes any call whose target is not a direct
    // function (virtual, delegate, native, COM, reflection resource lookup), triggers a type
    // initializer, or calls a method that may collect. Functions with no method identity (thunks,
    // dispatchers and other synthetic bodies) and methods the pass did not record may collect.
    //
    // The set is keyed by method identity (assembly, MethodDef row), not by function index: a pass
    // that publishes fewer roots may number its functions differently. Every specialization of a
    // method shares its key, so one that may collect makes all of them collect. The first pass
    // assumes everything may collect; its call edges give the set, and the next pass compiles with
    // it. The edges do not depend on the set, so that pass reproduces it. A set that still changes
    // after a few passes falls back to the conservative assumption, with one more pass.
    internal sealed class CollectionReach
    {
        // The set: usedBits[usedOffsets[assembly] + row]; null = everything may collect.
        private int[] usedOffsets, usedSizes;
        private bool[] usedBits;
        private bool conservative;
        private int[] owners, rows;       // this pass's function identities
        private int[] direct = new int[64];
        private int[] edgeFrom = new int[256], edgeTo = new int[256];
        private int edges, passes;

        internal bool MayCollect(int function)
        {
            if (usedBits == null || owners == null || function < 0 || function >= owners.Length) return true;
            int owner = owners[function], row = rows[function];
            if (row <= 0 || owner < 0 || owner >= usedSizes.Length || row >= usedSizes[owner]) return true;
            return usedBits[usedOffsets[owner] + row];
        }

        // Resets this pass's recorded information and binds its function identities.
        internal void BeginPass(int[] functionOwners, int[] functionRows)
        {
            owners = functionOwners;
            rows = functionRows;
            for (int i = 0; i < direct.Length; i++) direct[i] = 0;
            edges = 0;
        }

        // Records one compiled function's final records.
        internal void Record(int function, int[] records, int[] fieldInitializers, bool prologue)
        {
            if (function < 0) return;
            Grow(function + 1);
            int flags = 1; // recorded
            if (prologue) flags |= 2;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot], operand = records[slot + 1];
                if (kind == Call || kind == NewObject || kind == ConstructValue) AddEdge(function, operand);
                else if (kind == GarbageCollectorCall && operand != GarbageCollectorMembers.KeepAlive ||
                    kind == ResourceLookup || kind == InvokeDelegate || kind == CallNativeIndirect || kind == CallComCookieLookup ||
                    kind == CallDynamicNative || kind == CallPinnedCharacterNative || kind == VirtualCall || kind == CallNative ||
                    TypeInitializerWasm.HasFieldTrigger(kind, operand, fieldInitializers))
                    flags |= 2;
            }
            direct[function] |= flags;
        }

        // Computes the set from this pass. Returns true when another pass must compile with it.
        internal bool Settle(int functions)
        {
            if (conservative) return false;
            Grow(functions);
            var collects = new bool[functions];
            for (int f = 0; f < functions; f++)
                collects[f] = (direct[f] & 1) == 0 || (direct[f] & 2) != 0 || rows[f] <= 0;
            // Specializations of one method share its key: merge them before propagating.
            bool changed = true;
            while (changed)
            {
                changed = Merge(collects, functions);
                for (int e = 0; e < edges; e++)
                {
                    int from = edgeFrom[e], to = edgeTo[e];
                    if (from < functions && !collects[from] && (to < 0 || to >= functions || collects[to]))
                    {
                        collects[from] = true;
                        changed = true;
                    }
                }
            }
            bool[] next = Keyed(collects, functions);
            if (usedBits != null && Same(usedSizes, keyedSizes) && Same(usedBits, next)) return false;
            if (++passes > 3)
            {
                // Recompile once more with every method collecting.
                usedBits = null;
                conservative = true;
                return true;
            }
            usedBits = next;
            usedSizes = keyedSizes;
            usedOffsets = keyedOffsets;
            return true;
        }

        // Makes every function of a method collect when one of them does. Returns true on change.
        private bool Merge(bool[] collects, int functions)
        {
            bool[] keyed = Keyed(collects, functions);
            bool changed = false;
            for (int f = 0; f < functions; f++)
            {
                if (collects[f] || rows[f] <= 0 || owners[f] < 0) continue;
                if (keyed[keyedOffsets[owners[f]] + rows[f]]) { collects[f] = true; changed = true; }
            }
            return changed;
        }

        // One flag per (assembly, row) in a flat array at keyedOffsets[assembly] + row; also sets
        // keyedSizes (rows + 1 per assembly).
        private int[] keyedSizes, keyedOffsets;

        private bool[] Keyed(bool[] collects, int functions)
        {
            int assemblies = 0;
            for (int f = 0; f < functions; f++) if (owners[f] + 1 > assemblies) assemblies = owners[f] + 1;
            var sizes = new int[assemblies];
            var offsets = new int[assemblies];
            keyedSizes = sizes;
            keyedOffsets = offsets;
            for (int f = 0; f < functions; f++)
                if (owners[f] >= 0 && rows[f] + 1 > sizes[owners[f]]) sizes[owners[f]] = rows[f] + 1;
            int total = 0;
            for (int a = 0; a < assemblies; a++) { offsets[a] = total; total += sizes[a]; }
            var keyed = new bool[total];
            for (int f = 0; f < functions; f++)
                if (owners[f] >= 0 && rows[f] > 0 && collects[f]) keyed[offsets[owners[f]] + rows[f]] = true;
            return keyed;
        }

        private static bool Same(int[] left, int[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static bool Same(bool[] left, bool[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private void AddEdge(int from, int to)
        {
            if (edges == edgeFrom.Length)
            {
                var a = new int[edges * 2];
                var b = new int[edges * 2];
                for (int i = 0; i < edges; i++) { a[i] = edgeFrom[i]; b[i] = edgeTo[i]; }
                edgeFrom = a;
                edgeTo = b;
            }
            edgeFrom[edges] = from;
            edgeTo[edges] = to;
            edges++;
        }

        private void Grow(int count)
        {
            if (count <= direct.Length) return;
            int size = direct.Length;
            while (size < count) size *= 2;
            var grown = new int[size];
            for (int i = 0; i < direct.Length; i++) grown[i] = direct[i];
            direct = grown;
        }
    }
}
