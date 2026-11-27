using PeLibrary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // One planner per compilation. Only actual managed allocations can
    // contribute a runtime class to a dispatcher.
    internal sealed class VirtualDispatch
    {
        private const int Function = 0, Owner = 1, Row = 2, Receiver = 3,
            Declared = 4, CallerOwner = 5, CallerRow = 6, CallerOffset = 7, Scanned = 8, Head = 9, ScannedBoxes = 10, RootTarget = 11, SiteWidth = 12;
        private const int Class = 0, Target = 1, Next = 2, BoxElement = 3, CaseWidth = 4;

        private readonly byte[] known = new byte[MaxClasses];
        private readonly int[] allocations = new int[MaxClasses];
        private readonly int capacity;
        private readonly RuntimeTypeCalls typeCalls;
        private int[] sites = new int[0];
        private int[] cases = new int[0];
        private int allocationsCount, sitesUsed, casesUsed = CaseWidth, boxes;
        private int primitiveArrays;
        private bool typeDescriptors, unsupportedRootReceiver;

        internal VirtualDispatch(int capacity, RuntimeTypeCalls typeCalls)
        {
            // Both a function and each type-test arm use at least one output byte.
            this.capacity = capacity;
            this.typeCalls = typeCalls;
        }

        internal int BindShape(BundleImages bundle, int[] objects, int[] declared, int shape, int arity,
            int[] signatures, int[] offsets, int[] owners, int[] rows, int[] contexts, int target)
        {
            int status = typeCalls.BindVirtualSlot(bundle, objects, declared[shape], signatures, offsets[target],
                owners[target], rows[target], -1, contexts[target] != 0);
            if (status < 0) return status;
            for (int argument = 1; argument < arity; argument++)
            {
                status = typeCalls.BindVirtualSlot(bundle, objects, declared[shape + 1 + argument],
                    signatures, offsets[target] + 1 + argument, owners[target], rows[target], argument, contexts[target] != 0);
                if (status < 0) return status;
            }
            return 0;
        }

        internal void AddAllocation(int type)
        {
            int index = type - FirstClass;
            if (index < 0 || known[index] != 0) return;
            known[index] = 1;
            allocations[allocationsCount++] = type;
        }

        internal void AddBox(int element) => boxes |= 1 << element;
        internal void AddPrimitiveArray(int element) => primitiveArrays |= 1 << element;

        // Every produced evaluation-stack value is at the top on an incoming
        // edge. Special host objects without runtime identities leave the
        // root dispatch universe open.
        internal void ObserveReceiver(int type)
        {
            if (type == RuntimeTypeMetadata.Type) typeDescriptors = true;
            if (type == EventArgsReference || type == EventHandlerReference ||
                type == DelegateReference || type == SignatureTypes.Delegate || type == SignatureTypes.CharacterSpanCallback ||
                DelegateShapes.IsKey(type) ||
                type == RuntimeTypeMetadata.Module || type == RuntimeTypeMetadata.Assembly)
                unsupportedRootReceiver = true;
        }

        internal int Add(int function, int owner, int row, int receiver, int declared, int callerOwner, int callerRow, int pc)
        {
            for (int site = 0; site < sitesUsed; site += SiteWidth)
                if (sites[site + Owner] == owner && sites[site + Row] == row &&
                    sites[site + Receiver] == receiver && sites[site + Declared] == declared)
                    return sites[site + Function];

            if (sites.Length == 0)
            {
                sites = new int[capacity];
                cases = new int[capacity];
            }
            if (sitesUsed > sites.Length - SiteWidth) return -3;
            int at = sitesUsed;
            sitesUsed += SiteWidth;
            sites[at + Function] = function;
            sites[at + Owner] = owner;
            sites[at + Row] = row;
            sites[at + Receiver] = receiver;
            sites[at + Declared] = declared;
            sites[at + CallerOwner] = callerOwner;
            sites[at + CallerRow] = callerRow;
            sites[at + CallerOffset] = pc;
            return function;
        }

        internal int Expand(BundleImages bundle, int[] objects, int[] owners, int[] rows, int[] contexts, int[] bases,
            int[] indices, int[] arities, int[] offsets, int[] signatures, int functions, int[] location,
            MethodInstantiations instantiations)
        {
            for (int site = 0; site < sitesUsed; site += SiteWidth)
            {
                int receiver = sites[site + Receiver], dispatcher = sites[site + Function];
                if (receiver == ObjectReference)
                {
                    CompileLocation.Set(location, sites[site + CallerOwner], sites[site + CallerRow], sites[site + CallerOffset]);
                    if (unsupportedRootReceiver) return -2;
                    if (sites[site + RootTarget] == 0)
                    {
                        int root = MethodCompiler.BindVirtualBody(bundle, objects, ObjectReference, true, ObjectReference,
                            sites[site + Owner], sites[site + Row], owners, rows, contexts, bases, indices, arities, offsets, signatures, functions);
                        if (root < 0) return root;
                        if (root == functions) functions++;
                        sites[site + RootTarget] = root + 1;

                        int text = MethodCompiler.BindVirtualBody(bundle, objects, SignatureTypes.String, true, ObjectReference,
                            sites[site + Owner], sites[site + Row], owners, rows, contexts, bases, indices, arities, offsets, signatures, functions);
                        if (text < 0) return text;
                        if (text == functions) functions++;
                        int added = AddCase(site, SignatureTypes.String, text, 0);
                        if (added < 0) return added;
                    }
                }
                for (int index = sites[site + Scanned]; index < allocationsCount; index++)
                {
                    int type = allocations[index];
                    if (receiver != ObjectReference && ArrayElementKey(objects, type) != 0 &&
                        !IsExactArrayEnumerable(objects, type, sites[site + Declared]) &&
                        !IsExactArrayList(objects, type, sites[site + Declared])) continue;
                    if (!SignatureTypes.Assignable(type, receiver, objects)) continue;
                    CompileLocation.Set(location, sites[site + CallerOwner], sites[site + CallerRow], sites[site + CallerOffset]);
                    int shape = offsets[dispatcher];
                    int target = MethodCompiler.BindVirtualBody(bundle, objects, type, true, sites[site + Declared],
                        sites[site + Owner], sites[site + Row], owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, functions, instantiations);
                    if (target < 0) return target;
                    int signatureStatus = BindShape(bundle, objects, signatures, shape, arities[dispatcher], signatures, offsets,
                        owners, rows, contexts, target);
                    if (signatureStatus < 0) return signatureStatus;
                    if (!MethodCompiler.VirtualSignaturesMatch(arities, offsets, signatures, target,
                        signatures, shape, arities[dispatcher], type, objects)) return -2;
                    if (target == functions) functions++;
                    int added = AddCase(site, type, target, 0);
                    if (added < 0) return added;
                }
                sites[site + Scanned] = allocationsCount;
                if ((receiver == ObjectReference || IsInterface(objects, sites[site + Declared])) &&
                    boxes != sites[site + ScannedBoxes])
                {
                    functions = ExpandBoxes(bundle, objects, owners, rows, contexts, bases, indices, arities,
                        offsets, signatures, functions, location, site);
                    if (functions < 0) return functions;
                }
            }
            return functions;
        }

        internal void ObserveReceiverDemand(ReceiverBodyDemand demand, int[] contexts)
        {
            for (int site = 0; site < sitesUsed; site += SiteWidth)
                if (sites[site + RootTarget] != 0) demand.Require(sites[site + RootTarget] - 1, contexts);
            for (int entry = CaseWidth; entry < casesUsed; entry += CaseWidth)
                demand.Require(cases[entry + Target], contexts);
        }

        private int ExpandBoxes(BundleImages bundle, int[] objects, int[] owners, int[] rows, int[] contexts,
            int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int functions, int[] location, int site)
        {
            int declared = sites[site + Declared], dispatcher = sites[site + Function];
            int pending = boxes & ~sites[site + ScannedBoxes];
            bool root = declared == ObjectReference;
            var required = root ? null : new[] { AssemblyOf(objects, declared), TypeDef(objects, declared) };
            if (HasArguments(objects, declared))
            {
                sites[site + ScannedBoxes] = boxes;
                return functions;
            }
            for (int element = 2; element < 32; element++)
            {
                if ((pending & (1 << element)) == 0) continue;
                int primitive = GenericScalar.Key(element);
                if (!root)
                {
                    int implements = BundleConstraints.Implements(bundle, objects, primitive, required, 0);
                    if (implements < 0) return implements;
                    if (implements == 0) continue;
                }
                CompileLocation.Set(location, sites[site + CallerOwner], sites[site + CallerRow], sites[site + CallerOffset]);
                var identity = new int[2];
                int found = BundleConstraints.FindPrimitive(bundle, primitive, identity);
                if (found <= 0) return found < 0 ? found : -2;
                byte[] image = bundle.Get(identity[0]);
                var layout = new int[4];
                var heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int type = BundleLayouts.RegisterValue(bundle, image, layout, heaps, identity[0], identity[1], objects);
                if (type < 0) return type;
                if (!PrimitiveBoxing.HasPayloadField(objects, type, GenericScalar.Storage(primitive))) return -2;
                var method = new int[3];
                int selected = root
                    ? VirtualSlots.SelectVirtualBody(bundle, objects, type, true, declared, sites[site + Owner],
                        sites[site + Row], method, exactValue: true)
                    : InterfaceSlots.SelectBody(bundle, objects, type, declared, sites[site + Owner],
                        sites[site + Row], method, primitiveMap: true);
                if (selected < 0) return selected;
                if (method[0] != identity[0]) return -2;
                int target = MethodCompiler.Lookup(owners, rows, contexts, bases, indices, functions, method[0], method[1], type);
                if (target < 0)
                {
                    target = functions;
                    functions = MethodCompiler.Register(bundle, image, layout, heaps, method[0], method[1],
                        type, owners, rows, contexts, bases, indices, arities, offsets, signatures,
                        objects, functions, allowVirtual: true, receiverOverride: type);
                    if (functions < 0) return functions;
                }
                int shape = offsets[dispatcher];
                int result = BindShape(bundle, objects, signatures, shape, arities[dispatcher], signatures, offsets,
                    owners, rows, contexts, target);
                if (result < 0) return result;
                if (!MethodCompiler.VirtualSignaturesMatch(arities, offsets, signatures, target,
                    signatures, shape, arities[dispatcher], type, objects)) return -2;
                int added = AddCase(site, type, target, element);
                if (added < 0) return added;
            }
            sites[site + ScannedBoxes] = boxes;
            return functions;
        }

        private int AddCase(int site, int type, int target, int element)
        {
            if (casesUsed > cases.Length - CaseWidth) return -3;
            // Parent layouts always precede children. Test children first.
            int prior = 0;
            int next = sites[site + Head];
            while (next != 0 && cases[next + Class] > type)
            {
                prior = next;
                next = cases[next + Next];
            }
            int entry = casesUsed;
            casesUsed += CaseWidth;
            cases[entry + Class] = type;
            cases[entry + Target] = target;
            cases[entry + Next] = next;
            cases[entry + BoxElement] = element;
            if (prior == 0) sites[site + Head] = entry;
            else cases[prior + Next] = entry;
            return 0;
        }

        internal int Write(byte[] code, int[] bodyOffsets, int[] sizes, int[] arities, int[] offsets,
            int[] signatures, int[] objects, int length, int hostImports, int[] location, RuntimeReflectionPlan reflection,
            PrimitiveArrayWasm arrayHeaps)
        {
            int boxLayout = boxes == 0 ? 0 : PrimitiveBoxing.FindLayout(objects);
            for (int site = 0; site < sitesUsed; site += SiteWidth)
            {
                int function = sites[site + Function], arity = arities[function];
                CompileLocation.Set(location, sites[site + CallerOwner], sites[site + CallerRow], sites[site + CallerOffset]);
                bodyOffsets[function] = length;
                if (code.Length - length < 3) return -3;
                code[length++] = 0; // no additional locals
                for (int entry = sites[site + Head]; entry != 0; entry = cases[entry + Next])
                {
                    int type = cases[entry + Class], target = cases[entry + Target];
                    int element = cases[entry + BoxElement], armBytes = element == 0 ? 26 : 96;
                    if (code.Length - length < 2 || arity > (code.Length - length - 2) / 6 ||
                        code.Length - length - 2 - arity * 6 < armBytes) return -3;
                    int receiver = Representation(objects, signatures[offsets[target] + 1]);
                    length = element == 0
                        ? VirtualDispatchWasm.Arm(code, length, type, receiver, hostImports + target, arity)
                        : VirtualDispatchWasm.BoxArm(code, length, boxLayout, element, type, hostImports + target, arity);
                }

                if (sites[site + RootTarget] != 0)
                {
                    if (code.Length - length < 64) return -3;
                    if (typeDescriptors)
                        length = VirtualDispatchWasm.DescriptorArm(code, length, reflection);
                    int root = hostImports + sites[site + RootTarget] - 1;
                    length = VirtualDispatchWasm.ArmHeap(code, length, WasmBinary.RootObjectHeap, ObjectReference, root, arity);
                    for (int element = PrimitiveArrayWasm.FirstElement; element < PrimitiveArrayWasm.EndElement; element++)
                    {
                        if ((primitiveArrays & (1 << element)) == 0) continue;
                        if (code.Length - length < 26) return -3;
                        // Every CLI primitive vector inherits Object.ToString.
                        length = VirtualDispatchWasm.ArmHeap(code, length, element, ObjectReference, root, arity, arrayHeaps);
                    }
                }

                // The caller checked null. The fixed point covers reachable
                // managed allocations and boxes with supported slot bodies.
                code[length++] = 0x00; // unreachable
                code[length++] = 0x0b; // end
                sizes[function] = length - bodyOffsets[function];
            }
            return length;
        }
    }
}
