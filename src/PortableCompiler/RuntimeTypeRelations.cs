using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Each known receiver uses a predicate over canonical runtime Type
    // references. Unknown graph entries reach the explicit unsupported trap.
    internal sealed partial class RuntimeTypeRelations
    {
        private int[] targets = new int[4], functions = new int[4];
        private int count;

        internal static int LiteralReceiver(int[] records, int slot, int[] clauses, int[] source)
        {
            // The ordinary verifier still checks both stack operands as
            // descriptors. This proof establishes the non-null receiver's identity.
            int argument = Previous(records, slot, clauses);
            if (argument < 0) return -1;
            if (records[argument] == TypeFromHandle)
            {
                int token = Previous(records, argument, clauses);
                if (token < 0 || records[token] != LoadTypeToken) return -1;
                source[0] = records[token + 1];
                argument = token;
            }
            else if (records[argument] != Load && records[argument] != NullLiteral) return -1;
            int conversion = Previous(records, argument, clauses);
            if (conversion < 0 || records[conversion] != TypeFromHandle) return -1;
            int receiver = Previous(records, conversion, clauses);
            return receiver >= 0 && records[receiver] == LoadTypeToken ? records[receiver + 1] : -1;
        }

        internal static int Previous(int[] records, int slot, int[] clauses)
        {
            if (ValueOperands.Entry(records, slot / Width, clauses)) return -1;
            int previous = slot - Width;
            while (previous >= 0 && records[previous] == 0) previous -= Width;
            return previous >= 0 && records[previous + 2] * Width == slot ? previous : -1;
        }

        internal int Bind(BundleImages bundle, int[] objects, int[] globals, int target, int source,
            int[] owners, int[] arities, int[] offsets, int[] signatures, int used, int[] result)
        {
            int type = Nominal(bundle, objects, globals, target, true);
            // A closed generic destination may require interface variance.
            if (type < 0 || type >= FirstClass && HasArguments(objects, type)) return -2;
            if (source >= 0)
            {
                int argument = Nominal(bundle, objects, globals, source, true);
                if (argument < 0 || Compare(bundle, objects, argument, type) < 0) return -2;
            }
            for (int i = 0; i < count; i++)
                if (targets[i] == type)
                {
                    result[0] = functions[i];
                    return used;
                }

            int offset = MethodCompiler.NextSignature(offsets, arities, used);
            if (used >= owners.Length || signatures.Length - offset < 3) return -3;
            if (count == targets.Length)
            {
                var newTargets = new int[count * 2];
                var newFunctions = new int[count * 2];
                for (int i = 0; i < count; i++)
                {
                    newTargets[i] = targets[i];
                    newFunctions[i] = functions[i];
                }
                targets = newTargets;
                functions = newFunctions;
            }
            targets[count] = type;
            functions[count++] = used;
            arities[used] = 2;
            offsets[used] = offset;
            signatures[offset] = I32;
            signatures[offset + 1] = RuntimeTypeMetadata.Type;
            signatures[offset + 2] = RuntimeTypeMetadata.Type;
            result[0] = used;
            return used + 1;
        }

        private static int Nominal(BundleImages bundle, int[] objects, int[] globals, int descriptor, bool register)
        {
            if (descriptor < 0 || descriptor >= globals[0]) return -2;
            int owner = StaticFields.Owner(globals, descriptor), row = StaticFields.Row(globals, descriptor);
            if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < 0 || row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return -2;
            int module = RuntimeTypeTokens.ModuleSlot(owner) - 1;
            if (module < 0) return row == 0 ? ObjectReference : -2;
            if (module >= bundle.Count) return -2;
            int type;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
                type = row - RuntimeTypeTokens.ClosedTypeKey;
            else
            {
                type = Find(objects, module, row, new int[0]);
                // Literal operands may be the only uses of a nominal type.
                // Registration failure at that instruction remains located.
                if (type == 0 && register)
                {
                    byte[] image = bundle.Get(module);
                    var layout = new int[4];
                    var heaps = new int[8];
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                    type = BundleLayouts.RegisterReference(bundle, image, layout, heaps, module, row << 2, objects);
                }
            }
            return type >= FirstClass && type - FirstClass < objects[0] && AssemblyOf(objects, type) == module &&
                ArrayElementKey(objects, type) == 0 && !IsValueType(objects, type) ? type : -2;
        }

        private static int Compare(BundleImages bundle, int[] objects, int source, int target)
        {
            if (source == target || target == ObjectReference || Inherits(objects, source, target) ||
                Implements(objects, source, target)) return 1;
            if (!IsInterface(objects, target) || source == ObjectReference) return 0;
            // Positive paths already prove the edges they need. A missing
            // InterfaceImpl edge could invalidate a negative result.
            var layout = new int[4];
            var heaps = new int[8];
            for (int type = source; type >= FirstClass; type = Parent(objects, type))
                if (InterfaceSlots.CheckInterfaceMap(bundle, objects, type, layout, heaps) != 0) return -2;
            for (int type = FirstClass; type - FirstClass < objects[0]; type++)
                if (IsInterface(objects, type) && Implements(objects, source, type) &&
                    InterfaceSlots.CheckInterfaceMap(bundle, objects, type, layout, heaps) != 0) return -2;
            return 0;
        }

        internal int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals,
            int[] offsets, int[] sizes)
        {
            for (int i = 0; i < count; i++)
            {
                int function = functions[i], target = targets[i];
                offsets[function] = used;
                if (code.Length - used < 12) return -3;
                code[used++] = 0; // no local declarations
                used = Local(code, used, 0x20, 1);
                code[used++] = 0xd1; // ref.is_null
                used = ReturnCase(code, used, 0);
                // Object.GetType and the other reflection helpers can bind
                // descriptor globals after translating the calling body.
                for (int global = 0; global < globals[0]; global++)
                {
                    int source = Nominal(bundle, objects, globals, global, false);
                    if (source < 0) continue;
                    int assignable = Compare(bundle, objects, source, target);
                    if (assignable < 0) continue;
                    if (code.Length - used < 16) return -3;
                    used = Local(code, used, 0x20, 1);
                    code[used++] = 0x23; // global.get
                    used = WasmBinary.Unsigned(code, used, global);
                    code[used++] = 0xd3; // ref.eq
                    used = ReturnCase(code, used, assignable);
                }
                if (code.Length - used < 2) return -3;
                code[used++] = 0x00; // unsupported descriptor graph
                code[used++] = 0x0b;
                sizes[function] = used - offsets[function];
            }
            return used;
        }

        private static int ReturnCase(byte[] code, int used, int value)
        {
            code[used++] = 0x04; // if (void)
            code[used++] = 0x40;
            used = Const(code, used, value);
            code[used++] = 0x0f; // return
            code[used++] = 0x0b;
            return used;
        }
    }
}
