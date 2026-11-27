using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Stage 129 (decision 0057). Each reached type initializer becomes an
    // ordinary Wasm function guarded by one mutable i32 global:
    //   0 = not started; nonzero = started (running or finished).
    // On the single managed thread (decision 0026) a trigger during the
    // initializer's own execution returns immediately, as the CLR does for
    // recursive initialization on the same thread. An exception that escapes the
    // initializer traps instead of reaching managed code (no TypeInitializationException yet).
    internal sealed class TypeInitializerPlan
    {
        // Initializer function slot -> guard global + 1.
        private readonly int[] guards;
        // Static field global -> initializer function + 1 to call before each access.
        private readonly int[] fields;
        // The module's failure epoch global (see TypeInitializerWasm), or -1.
        private int epoch = -1;
        // Count of initializers on the Wasm stack, checked at host export entry.
        private int active = -1;

        internal TypeInitializerPlan(int functions, int globals)
        {
            guards = new int[functions];
            fields = new int[globals];
        }

        internal int[] FieldInitializers => fields;

        internal int Guard(int function) => guards[function] - 1;

        internal int Epoch => epoch;

        internal int Active => active;

        internal void SetFieldInitializer(int global, int initializer) => fields[global] = initializer + 1;

        // Register the initializer function and its guard global, once per type.
        // Returns the updated function count; initializer[0] receives the slot.
        internal int Ensure(BundleImages bundle, int module, int type, int row,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities,
            int[] offsets, int[] signatures, int[] objects, int[] globals, int functions, int[] initializer, int context = 0)
        {
            int existing = MethodCompiler.Lookup(owners, rows, contexts, bases, indices, functions, module, row, context);
            if (existing < 0)
            {
                byte[] image = bundle.Get(module);
                var layout = new int[4];
                var heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                functions = MethodCompiler.Register(bundle, image, layout, heaps, module, row, context, owners, rows, contexts,
                    bases, indices, arities, offsets, signatures, objects, functions, initializer: true);
                if (functions < 0) return functions;
                existing = functions - 1;
            }
            if (existing >= guards.Length) return -3;
            if (epoch < 0)
            {
                // Owner 0, row 0 is never a field or a type guard.
                epoch = StaticFields.Register(globals, 0, 0, I32);
                if (epoch < 0) return epoch;
                // Type guards use rows -1 down to -TypeDef count; int.MinValue is free.
                active = StaticFields.Register(globals, 0, int.MinValue, I32);
                if (active < 0) return active;
            }
            if (guards[existing] == 0)
            {
                // Negative rows never name a field, so the guard cannot alias static storage.
                int global = StaticFields.Register(globals, module, -type, I32, context);
                if (global < 0) return global;
                if (global >= fields.Length) return -3;
                guards[existing] = global + 1;
            }
            initializer[0] = existing;
            return functions;
        }
    }
}
