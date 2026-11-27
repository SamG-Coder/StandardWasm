using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // The selected CLR template fixes the generic definition. A reachable
    // argument descriptor selects a closed layout and its original constructor.
    internal sealed class RuntimeGenericActivation
    {
        private readonly int owner, row, constructor, function;
        private int scanned, count;
        private int[] cases = new int[12];
        private const int Width = 3;

        internal RuntimeGenericActivation(int owner, int row, int constructor, int function)
        {
            this.owner = owner;
            this.row = row;
            this.constructor = constructor;
            this.function = function;
        }

        internal static int Template(int[] records, int call, int[] clauses)
        {
            int argument = RuntimeTypeRelations.Previous(records, call, clauses);
            if (argument < 0 || records[argument] != BranchIl.Load) return -1;
            int cast = RuntimeTypeRelations.Previous(records, argument, clauses);
            if (cast < 0 || records[cast] != BranchIl.CastClass || records[cast + 1] != RuntimeTypeMetadata.Type) return -1;
            int conversion = RuntimeTypeRelations.Previous(records, cast, clauses);
            if (conversion < 0 || records[conversion] != BranchIl.TypeFromHandle) return -1;
            int token = RuntimeTypeRelations.Previous(records, conversion, clauses);
            return token >= 0 && records[token] == BranchIl.LoadTypeToken ? records[token + 1] : -1;
        }

        internal static RuntimeGenericActivation Bind(BundleImages bundle, int[] objects, int[] globals, int global, int function)
        {
            int descriptor = StaticFields.Owner(globals, global), row = StaticFields.Row(globals, global);
            if (!RuntimeTypeTokens.IsDescriptorOwner(descriptor) || row <= 0 || row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return null;
            int owner = RuntimeTypeTokens.ModuleSlot(descriptor) - 1;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                if (ArgumentCount(objects, type) != 1) return null;
                row = TypeDef(objects, type);
                owner = AssemblyOf(objects, type);
            }
            if (owner < 0 || owner >= bundle.Count) return null;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return null;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return null;
            int[] parameters = GenericParameters.TypeParameterNames(image, heaps, layout, counts, row);
            if (parameters == null || parameters.Length != 1) return null;
            int constructor = GenericActivationMetadata.PublicDefaultConstructor(image, layout, heaps, row);
            return constructor > 0 ? new RuntimeGenericActivation(owner, row, constructor, function) : null;
        }

        internal int Expand(BundleImages bundle, int[] objects, int[] globals, int[] owners, int[] rows, int[] contexts,
            int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int functions,
            FinalizerSlots finalizers, VirtualDispatch dispatch)
        {
            if (scanned == globals[0]) return functions;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int[] core = RuntimeGenericTypes.CoreTypes(bundle);
            var empty = new int[0];
            var arguments = new int[1];
            var deferred = new int[8];
            var finalizer = new int[2];
            while (scanned < globals[0])
            {
                int global = scanned++;
                int publication = PublishArguments(bundle, objects, globals, global);
                if (publication < 0) return publication;
                int argument = RuntimeGenericTypes.Argument(bundle, objects, globals, global, core, empty);
                if (argument <= 0) continue;
                arguments[0] = argument;
                int constraint = GenericParameters.Check(image, heaps, layout, row, arguments, objects, deferred);
                if (constraint > 0)
                    constraint = BundleConstraints.Check(bundle, image, layout, heaps, owner, objects, arguments, deferred);
                if (constraint == -2) continue;
                if (constraint < 0) return constraint;
                int type = BundleLayouts.RegisterInstantiation(bundle, image, layout, heaps, owner, row << 2, objects, arguments, false);
                if (type < 0) return type;
                if (IsAbstract(objects, type)) continue;
                // A finalizable allocation also needs its registered finalizer.
                // Reject it until this helper can bind that callback.
                if (finalizers != null && finalizers.Includes(bundle, objects, type, finalizer) != 0) return -2;
                dispatch.AddAllocation(type);
                int target = MethodCompiler.Lookup(owners, rows, contexts, bases, indices, functions, owner, constructor, type);
                if (target < 0)
                {
                    functions = MethodCompiler.Register(bundle, image, layout, heaps, owner, constructor, type,
                        owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    target = functions - 1;
                }
                if (arities[target] != 1 || signatures[offsets[target]] != Void || signatures[offsets[target] + 1] != type)
                    return -2;
                if (count == cases.Length)
                {
                    var grown = new int[count * 2];
                    for (int i = 0; i < count; i++) grown[i] = cases[i];
                    cases = grown;
                }
                cases[count++] = global;
                cases[count++] = type;
                cases[count++] = target;
            }
            return functions;
        }

        private static int PublishArguments(BundleImages bundle, int[] objects, int[] globals, int global)
        {
            int owner = StaticFields.Owner(globals, global), row = StaticFields.Row(globals, global);
            if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < RuntimeTypeTokens.ClosedTypeKey ||
                row >= RuntimeTypeTokens.PrimitiveVectorKey) return 0;
            int type = row - RuntimeTypeTokens.ClosedTypeKey;
            if (type < FirstClass || type - FirstClass >= objects[0]) return -1;
            for (int i = 0; i < ArgumentCount(objects, type); i++)
            {
                // GetGenericArguments can expose a type that has no ldtoken.
                // Publish that identity before activation collects constructors.
                int result = RuntimeTypeTokens.BindLayout(bundle, objects, Argument(objects, type, i), globals);
                if (result < 0 && result != -2) return result;
            }
            return 0;
        }

        internal int Write(byte[] code, int used, int[] offsets, int[] sizes, int hostImports, int rootCount)
        {
            offsets[function] = used;
            if (code.Length - used < 32) return -3;
            code[used++] = rootCount < 0 ? (byte)1 : (byte)2;
            code[used++] = 1; code[used++] = 0x6d; // eqref receiver
            if (rootCount >= 0) { code[used++] = 1; code[used++] = 0x7f; } // caller root frame
            used = GuardNull(code, used, 1);
            for (int start = 0; start < count; start += Width)
            {
                if (code.Length - used < (rootCount < 0 ? 52 : 176)) return -3;
                used = Local(code, used, 0x20, 1);
                code[used++] = 0x23;
                used = WasmBinary.Unsigned(code, used, cases[start]);
                code[used++] = 0xd3; // canonical argument identity
                code[used++] = 0x04; code[used++] = 0x40;
                int type = cases[start + 1];
                used = Gc(code, used, 1, type); // struct.new_default
                used = Local(code, used, 0x21, 2);
                if (rootCount >= 0)
                {
                    used = RootStackWasm.Enter(code, used, rootCount, 3, 1);
                    used = RootStackWasm.Store(code, used, 3, 0, 2);
                    code[used++] = 0x02; code[used++] = ExceptionDispatchWasm.ExceptionReference;
                    code[used++] = 0x1f; code[used++] = 0x40; code[used++] = 1;
                    code[used++] = 0x03; code[used++] = 0; // catch_all_ref
                }
                used = Local(code, used, 0x20, 2);
                code[used++] = 0xfb;
                code[used++] = 22; // ref.cast to the constructor receiver
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + cases[start + 2]);
                if (rootCount >= 0) used = RootStackWasm.Leave(code, used, rootCount, 3);
                used = Local(code, used, 0x20, 2);
                code[used++] = 0x0f;
                if (rootCount >= 0)
                {
                    code[used++] = 0x0b; // end try_table
                    code[used++] = 0x00;
                    code[used++] = 0x0b; // caught exnref
                    used = RootStackWasm.Leave(code, used, rootCount, 3);
                    code[used++] = 0x0a; // throw_ref
                }
                code[used++] = 0x0b;
            }
            if (code.Length - used < 2) return -3;
            code[used++] = 0x00; // No AOT constructor for this descriptor.
            code[used++] = 0x0b;
            sizes[function] = used - offsets[function];
            return used;
        }
    }
}
