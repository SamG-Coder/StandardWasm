using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class RuntimeTypeBranches
    {
        // Fold branches over closed typeof expressions and canonical Type
        // tests. No other instruction can enter the test
        // sequence after its first operand, so replacing it preserves stack depth.
        internal static void FoldClosedTypeBranches(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] frame, int[] records, int[] clauses, int methodCount)
        {
            FoldReferenceContainment(bundle, image, layout, heaps, owner, context, methodArguments, objects, records, clauses);
            if ((methodArguments == null && context == 0) || clauses.Length != 0) return;
            bool changed = false;
            var left = new int[2];
            var right = new int[2];
            for (int first = 0; first < records.Length; first += Width)
            {
                if (records[first] != LoadTypeToken) continue;
                int leftCall = Next(records, first);
                if (!GetTypeCall(image, layout, heaps, records, leftCall, methodCount)) continue;
                int second = Next(records, leftCall);
                if (second >= records.Length) continue;
                int cast = -1, store = -1, reload = -1;
                if (records[second] == CastClass &&
                    RuntimeTypeMetadata.IsRuntimeTypeDefinition(image, heaps, layout, records[second + 1]))
                    cast = second;
                int candidate = cast < 0 ? second : Next(records, cast);
                if (candidate < records.Length && records[candidate] == Store)
                {
                    int load = Next(records, candidate);
                    if (load < records.Length && records[load] == Load && records[load + 1] == records[candidate + 1])
                    {
                        store = candidate;
                        reload = load;
                        second = Next(records, load);
                    }
                }
                if (second >= records.Length) continue;
                if (records[second] != LoadTypeToken)
                {
                    if (store < 0 && FoldTypePropertyBranch(bundle, image, layout, heaps, owner, context, methodArguments, objects,
                        frame, records, clauses, first, leftCall, second, methodCount)) changed = true;
                    continue;
                }
                int rightCall = Next(records, second);
                if (!GetTypeCall(image, layout, heaps, records, rightCall, methodCount)) continue;
                int compare = Next(records, rightCall);
                int operation = TypeComparison(image, layout, heaps, records, compare, methodCount);
                if (operation != 2 && operation != 3) continue;
                int branch = Next(records, compare);
                if (branch >= records.Length || records[branch] != Conditional ||
                    (records[branch + 3] != 0x45 && records[branch + 3] != 0)) continue;
                if (ValueOperands.Entry(records, leftCall / Width, clauses) ||
                    cast >= 0 && ValueOperands.Entry(records, cast / Width, clauses) ||
                    store >= 0 && (ValueOperands.Entry(records, store / Width, clauses) ||
                        ValueOperands.Entry(records, reload / Width, clauses)) ||
                    ValueOperands.Entry(records, second / Width, clauses) ||
                    ValueOperands.Entry(records, rightCall / Width, clauses) ||
                    ValueOperands.Entry(records, compare / Width, clauses) ||
                    ValueOperands.Entry(records, branch / Width, clauses)) continue;
                if (RuntimeTypeTokens.ReadIdentity(bundle, image, layout, heaps, owner, records[first + 1],
                    context, objects, methodArguments, left) < 0 ||
                    RuntimeTypeTokens.ReadIdentity(bundle, image, layout, heaps, owner, records[second + 1],
                    context, objects, methodArguments, right) < 0) continue;

                bool equal = left[0] == right[0] && left[1] == right[1];
                bool result = operation == 2 ? equal : !equal;
                bool taken = records[branch + 3] == 0x45 ? !result : result;
                records[second] = Nop;
                records[rightCall] = Nop;
                records[compare] = Nop;
                records[branch] = taken ? Branch : Nop;
                records[branch + 3] = 0;
                if (store >= 0)
                {
                    // Fold the independent comparison first. The producer is
                    // removable only when no live code can read or address its local.
                    records[reload] = Nop;
                    RemoveUnreachable(records);
                    if (LocalIsRead(records, records[store + 1])) continue;
                    records[store] = Nop;
                    if (cast >= 0) records[cast] = Nop;
                }
                records[first] = Nop;
                records[leftCall] = Nop;
                changed = true;
            }
            if (changed) RemoveUnreachable(records);
        }

        private static bool LocalIsRead(int[] records, int local)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if ((kind == Load || kind == FrameAddress) && records[slot + 1] == local) return true;
            }
            return false;
        }

        // A closed sizeof is a literal CLI size. Fold only an independent
        // two-operand equality branch so it cannot retain an impossible
        // machine-width arm or change an incoming evaluation stack.
        internal static void FoldClosedSizeBranches(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] methodArguments, int[] objects, int[] records, int[] clauses)
        {
            if ((context == 0 && methodArguments == null) || clauses.Length != 0) return;
            bool changed = false;
            for (int source = 0; source < records.Length; source += Width)
            {
                if (records[source] != SizeOf) continue;
                int literal = Next(records, source);
                if (literal >= records.Length || records[literal] != Constant) continue;
                int branch = Next(records, literal);
                if (branch >= records.Length || records[branch] != Conditional) continue;
                int comparison = records[branch + 3];
                if ((comparison != 0x46 && comparison != 0x47) ||
                    ValueOperands.Entry(records, literal / Width, clauses) ||
                    ValueOperands.Entry(records, branch / Width, clauses)) continue;
                int size = ManagedSizeOf.Read(bundle, image, layout, heaps, owner,
                    records[source + 1], context, objects, methodArguments);
                if (size < 0) continue;

                bool equal = size == records[literal + 1];
                bool taken = comparison == 0x46 ? equal : !equal;
                records[source] = Nop;
                records[literal] = Nop;
                records[branch] = taken ? Branch : Nop;
                records[branch + 3] = 0;
                changed = true;
            }
            if (changed) RemoveUnreachable(records);
        }

        private static void FoldReferenceContainment(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] methodArguments, int[] objects, int[] records, int[] clauses)
        {
            int[] counts = null, spec = null, arguments = null;
            bool prune = false;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != MethodSpecCall || (records[slot + 3] & MethodSpecCallvirt) != 0) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (PeLibrary.MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                    spec = new int[MethodSpecifications.Width];
                    arguments = new int[1];
                }
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[slot + 1], spec) < 0 ||
                    !ReferenceContainment.IsIntrinsic(image, layout, heaps, counts, spec) ||
                    MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context,
                        objects, methodArguments, spec, arguments) < 0) continue;

                int result = ReferenceContainment.Check(objects, arguments[0]);
                if (result < 0) continue;
                int branch = Next(records, slot);
                // Exception handlers enter from metadata, outside the normal-flow reachability walk.
                if (clauses.Length == 0 && branch < records.Length && records[branch] == Conditional &&
                    (records[branch + 3] == 0x45 || records[branch + 3] == 0) &&
                    !ValueOperands.Entry(records, branch / Width, clauses))
                {
                    bool taken = records[branch + 3] == 0x45 ? result == 0 : result != 0;
                    records[slot] = Nop;
                    records[branch] = taken ? Branch : Nop;
                    records[branch + 3] = 0;
                    prune = true;
                }
                else
                {
                    records[slot] = Constant;
                    records[slot + 1] = result;
                    records[slot + 3] = 0;
                }
            }
            if (prune) RemoveUnreachable(records);
        }

        private static int Next(int[] records, int slot) => records[slot + 2] * Width;

        private static bool IsStaticMember(int[] records, int slot) =>
            slot < records.Length && records[slot] == MemberCall && records[slot + 3] == 0x28;

        private static bool GetTypeCall(byte[] image, int[] layout, int[] heaps, int[] records, int slot, int methodCount) =>
            slot < records.Length && (records[slot] == Call && records[slot + 3] == 0
                ? records[slot + 1] > 0 && records[slot + 1] <= methodCount &&
                    RuntimeTypeMetadata.IsCoreGetTypeFromHandle(image, heaps, layout, records[slot + 1])
                : IsStaticMember(records, slot) && RuntimeTypeMetadata.IsGetTypeFromHandle(image, heaps, layout, records[slot + 1]));

        private static int TypeComparison(byte[] image, int[] layout, int[] heaps, int[] records, int slot, int methodCount)
        {
            if (IsStaticMember(records, slot))
                return RuntimeTypeMetadata.ObjectTypeMember(image, heaps, layout, records[slot + 1]);
            if (slot >= records.Length || records[slot] != Call || records[slot + 3] != 0) return 0;
            int row = records[slot + 1];
            return row > 0 && row <= methodCount ? RuntimeTypeMetadata.CoreTypeComparison(image, heaps, layout, row) : 0;
        }

        private static bool FoldTypePropertyBranch(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] frame, int[] records, int[] clauses,
            int first, int handle, int getter, int methodCount)
        {
            int coded = records[first + 1];
            if ((coded & 3) != 2 || records[getter] != Call && records[getter] != MemberCall) return false;
            int property = RuntimeTypeMetadata.ClosedTypeProperty(image, heaps, layout,
                records[getter + 1], records[getter] == Call);
            if (property == 0) return false;
            if (ValueOperands.Entry(records, handle / Width, clauses) ||
                ValueOperands.Entry(records, getter / Width, clauses)) return false;
            int type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                coded >> 2, context, objects, methodArguments);
            bool isEnum = ObjectLayouts.IsEnumKey(type);
            bool isValue = GenericScalar.IsScalar(type) || isEnum || ObjectLayouts.IsValueType(objects, type);
            if (!isValue && type != SignatureTypes.String && type != ObjectLayouts.ObjectReference &&
                (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0])) return false;

            bool result = property == RuntimeTypeMetadata.EnumProperty ? isEnum : isValue;
            if (!FoldBooleanBranch(image, layout, heaps, frame, records, clauses, getter, methodCount, result)) return false;
            records[first] = Nop;
            records[handle] = Nop;
            records[getter] = Nop;
            return true;
        }

        private static bool FoldBooleanBranch(byte[] image, int[] layout, int[] heaps, int[] frame, int[] records,
            int[] clauses, int getter, int methodCount, bool result)
        {
            int branch = Next(records, getter);
            int literal = -1, equality = -1, combine = -1;
            if (branch < records.Length && records[branch] == Constant &&
                (uint)records[branch + 1] <= 1)
            {
                int comparison = Next(records, branch);
                if (comparison < records.Length && records[comparison] == Binary && records[comparison + 1] == 0x46)
                {
                    if (ValueOperands.Entry(records, branch / Width, clauses) ||
                        ValueOperands.Entry(records, comparison / Width, clauses)) return false;
                    literal = branch;
                    equality = comparison;
                    result = result == (records[literal + 1] == 1);
                    branch = Next(records, equality);
                }
            }
            if (branch < records.Length && records[branch] != Conditional)
            {
                int binary = Next(records, branch);
                // A constant false & x or true | x fixes the zero/nonzero
                // decision. Keep x evaluated, even when it calls a method.
                if (binary >= records.Length || records[binary] != Binary ||
                    records[binary + 1] != (result ? 0x72 : 0x71) ||
                    ValueOperands.Entry(records, branch / Width, clauses) ||
                    ValueOperands.Entry(records, binary / Width, clauses) ||
                    !ProducesInt32(image, layout, heaps, frame, records, branch, methodCount)) return false;
                combine = binary;
                branch = Next(records, combine);
            }
            if (branch >= records.Length || records[branch] != Conditional ||
                (records[branch + 3] != 0x45 && records[branch + 3] != 0) ||
                ValueOperands.Entry(records, branch / Width, clauses)) return false;

            if (literal >= 0)
            {
                records[literal] = Nop;
                records[equality] = Nop;
            }
            if (combine >= 0) records[combine] = Pop;
            bool taken = records[branch + 3] == 0x45 ? !result : result;
            records[branch] = taken ? Branch : Nop;
            records[branch + 3] = 0;
            return true;
        }

        private static bool ProducesInt32(byte[] image, int[] layout, int[] heaps, int[] frame, int[] records,
            int slot, int methodCount)
        {
            int kind = records[slot], operand = records[slot + 1], call = records[slot + 3];
            if (kind == Constant) return true;
            if (kind == Load) return SignatureTypes.StackType(frame[operand]) == SignatureTypes.I32;
            int signature;
            if (kind == Call && call == 0 && operand > 0 && operand <= methodCount)
                signature = MethodDefinitions.ReadColumn(image, layout, operand, 4);
            else if (kind == MemberCall && call == 0x28)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, operand, reference, allowClosedParent: true) < 0)
                    return false;
                signature = reference[5];
            }
            else return false;

            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, signature, 0, cursor);
            int start = cursor[0];
            return end >= 0 && end - start == 1 &&
                (image[start] == SignatureReader.Boolean || image[start] == SignatureReader.Int32 || image[start] == 9);
        }

        internal static void RemoveUnreachable(int[] records)
        {
            var reachable = new byte[records.Length / Width];
            var pending = new int[reachable.Length];
            int count = Visit(reachable, pending, 0, 0);
            for (int item = 0; item < count; item++)
            {
                int slot = pending[item] * Width;
                int kind = records[slot];
                if (kind == Return || kind == ThrowNull || kind == Rethrow || kind == EndFinally) continue;
                if (kind == Branch || kind == Conditional || kind == Leave)
                    count = Visit(reachable, pending, count, records[slot + 1]);
                if (kind == Branch || kind == Leave) continue;
                if (kind == Switch)
                    for (int i = 0; i < records[slot + 3]; i++)
                        count = Visit(reachable, pending, count, SwitchTarget(records, records[slot + 1], i));
                count = Visit(reachable, pending, count, records[slot + 2]);
            }
            // Switch tables and fused operations can store operands in unused
            // record slots. Clear only the instruction kind of a dead record.
            for (int pc = 0; pc < reachable.Length; pc++)
                if (reachable[pc] == 0) records[pc * Width] = 0;
        }

        private static int Visit(byte[] reachable, int[] pending, int count, int target)
        {
            if ((uint)target >= (uint)reachable.Length || reachable[target] != 0) return count;
            pending[count] = target;
            reachable[target] = 1;
            return count + 1;
        }
    }
}
