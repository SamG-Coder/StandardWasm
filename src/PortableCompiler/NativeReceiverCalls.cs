using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A value method can have both a traced and a linear-memory receiver.
    // Reserve a separate key so the two bodies never share an ABI.
    internal static class NativeReceiverCalls
    {
        private const int KeyBase = ObjectLayouts.FirstClass + ObjectLayouts.MaxClasses;

        internal static bool IsNativeKey(int key) => key >= KeyBase && key != CoreVersionByref.ContextKey;

        internal static int DeclaringContext(int key) =>
            key == CoreVersionByref.ContextKey ? 0 : IsNativeKey(key) ? key - KeyBase : key < 0 ? ~key : key;

        internal static bool UsesNativeReceiver(int key, int[] stack, int start) =>
            key < 0 && start >= 0 && IsNativeInteger(stack[start]);

        internal static int Bind(BundleImages bundle, int[] records, int[] depths, int[] states, int maximum,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int[] objects, int functions, int[] location, MethodInstantiations instantiations,
            int[] scopedCalls)
        {
            for (int pc = 0; pc < depths.Length; pc++)
            {
                int slot = pc * BranchIl.Width;
                if (depths[pc] == 0 || records[slot] != BranchIl.Call || records[slot + 3] != 0) continue;
                int target = records[slot + 1], start = depths[pc] - 1 - arities[target];
                if (start < 0 || !UsesNativeReceiver(contexts[target], states, pc * maximum + start)) continue;
                if (location.Length != 0) location[2] = pc;
                int type = LocatedValue(signatures[offsets[target] + 1]);
                bool scoped = ObjectLayouts.IsByRefLikeValue(objects, type);
                if (scoped && (scopedCalls == null || scopedCalls[pc] == 0)) return -2;
                int owner = owners[target], row = rows[target], key = KeyBase + ~contexts[target];
                int[] methodArguments = instantiations.Arguments(target);
                int native = methodArguments == null
                    ? MethodCompiler.Lookup(owners, rows, contexts, bases, indices, functions, owner, row, key)
                    : instantiations.Find(owners, rows, contexts, functions, owner, row, key, methodArguments);
                if (native < 0)
                {
                    int definingAssembly = ObjectLayouts.AssemblyOf(objects, type);
                    int definition = ObjectLayouts.TypeDef(objects, type);
                    var size = new int[2];
                    int measured = scoped ? NativeValueLayouts.MeasureFrame(bundle, definingAssembly, definition, size) :
                        NativeValueLayouts.Measure(bundle, definingAssembly, definition, size);
                    if (measured < 0) return measured;
                    byte[] image = bundle.Get(owner);
                    var layout = new int[4];
                    var heaps = new int[8];
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                    native = functions;
                    functions = MethodCompiler.Register(bundle, image, layout, heaps, owner, row, key,
                        owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions,
                        methodArguments: methodArguments);
                    if (functions < 0) return functions;
                    if (methodArguments != null) instantiations.Record(native, owners.Length, methodArguments);
                }
                // The verifier already checked the CLI operands. Preserve all
                // of their types when changing the receiver's representation.
                if (arities[target] != arities[native]) return -2;
                for (int operand = 0; operand <= arities[target]; operand++)
                    if (operand != 1 && signatures[offsets[target] + operand] != signatures[offsets[native] + operand])
                        return -2;
                records[slot + 1] = native;
            }
            return functions;
        }
    }
}
