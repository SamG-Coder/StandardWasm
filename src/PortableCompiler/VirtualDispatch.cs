using PeLibrary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // One planner per compilation. Only actual managed allocations can
    // contribute a runtime class to a dispatcher.
    internal sealed class VirtualDispatch
    {
        private const int Function = 0, Owner = 1, Row = 2, Receiver = 3,
            Declared = 4, CallerOwner = 5, CallerRow = 6, CallerOffset = 7, Scanned = 8, Head = 9, SiteWidth = 10;
        private const int Class = 0, Target = 1, Next = 2, CaseWidth = 3;

        private readonly byte[] known = new byte[MaxClasses];
        private readonly int[] allocations = new int[MaxClasses];
        private readonly int capacity;
        private int[] sites = new int[0];
        private int[] cases = new int[0];
        private int allocationsCount, sitesUsed, casesUsed = CaseWidth;

        internal VirtualDispatch(int capacity)
        {
            // Both a function and each type-test arm use at least one output byte.
            this.capacity = capacity;
        }

        internal void AddAllocation(int type)
        {
            int index = type - FirstClass;
            if (index < 0 || known[index] != 0) return;
            known[index] = 1;
            allocations[allocationsCount++] = type;
        }

        internal int Add(int function, int owner, int row, int receiver, int declared, int callerOwner, int callerRow, int pc)
        {
            for (int site = 0; site < sitesUsed; site += SiteWidth)
                if (sites[site + Owner] == owner && sites[site + Row] == row && sites[site + Receiver] == receiver)
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
            int[] indices, int[] arities, int[] offsets, int[] signatures, int functions, int[] location)
        {
            for (int site = 0; site < sitesUsed; site += SiteWidth)
            {
                int receiver = sites[site + Receiver], dispatcher = sites[site + Function];
                for (int index = sites[site + Scanned]; index < allocationsCount; index++)
                {
                    int type = allocations[index];
                    if (!SignatureTypes.Assignable(type, receiver, objects)) continue;
                    CompileLocation.Set(location, sites[site + CallerOwner], sites[site + CallerRow], sites[site + CallerOffset]);
                    int shape = offsets[dispatcher];
                    int target = MethodCompiler.BindVirtualBody(bundle, objects, type, true, sites[site + Declared],
                        sites[site + Owner], sites[site + Row], owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, functions);
                    if (target < 0) return target;
                    if (!MethodCompiler.VirtualSignaturesMatch(arities, offsets, signatures, target,
                        signatures, shape, arities[dispatcher], type, objects)) return -2;
                    if (target == functions) functions++;
                    if (casesUsed > cases.Length - CaseWidth) return -3;

                    // Parent layouts always precede children. A subtype test
                    // for a child must run before a test for its base.
                    int prior = 0;
                    int next = sites[site + Head];
                    while (next != 0 && cases[next + Class] > type)
                    {
                        prior = next;
                        next = cases[next + Next];
                    }
                    int at = casesUsed;
                    casesUsed += CaseWidth;
                    cases[at + Class] = type;
                    cases[at + Target] = target;
                    cases[at + Next] = next;
                    if (prior == 0) sites[site + Head] = at;
                    else cases[prior + Next] = at;
                }
                sites[site + Scanned] = allocationsCount;
            }
            return functions;
        }

        internal int Write(byte[] code, int[] bodyOffsets, int[] sizes, int[] arities, int[] offsets,
            int[] signatures, int length, int hostImports, int[] location)
        {
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
                    if (code.Length - length < 2 || arity > (code.Length - length - 2) / 6 ||
                        code.Length - length - 2 - arity * 6 < 26) return -3;
                    length = VirtualDispatchWasm.Arm(code, length, type, signatures[offsets[target] + 1],
                        hostImports + target, arity);
                }

                // The caller checked null. All reachable non-null managed
                // classes are covered by the completed allocation fixed point.
                code[length++] = 0x00; // unreachable
                code[length++] = 0x0b; // end
                sizes[function] = length - bodyOffsets[function];
            }
            return length;
        }
    }
}
