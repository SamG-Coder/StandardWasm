using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class RuntimeTypeBranches
    {
        // Fold only two direct typeof expressions followed by a platform Type
        // equality test and a branch. No other instruction can enter the test
        // sequence after its first operand, so replacing it preserves stack depth.
        internal static void FoldClosedTypeBranches(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int[] clauses)
        {
            if (methodArguments == null || clauses.Length != 0) return;
            bool changed = false;
            var left = new int[2];
            var right = new int[2];
            for (int first = 0; first < records.Length; first += Width)
            {
                if (records[first] != LoadTypeToken) continue;
                int leftCall = Next(records, first);
                if (!GetTypeCall(image, layout, heaps, records, leftCall)) continue;
                int second = Next(records, leftCall);
                if (second >= records.Length || records[second] != LoadTypeToken) continue;
                int rightCall = Next(records, second);
                if (!GetTypeCall(image, layout, heaps, records, rightCall)) continue;
                int compare = Next(records, rightCall);
                if (!IsStaticMember(records, compare)) continue;
                int operation = RuntimeTypeMetadata.ObjectTypeMember(image, heaps, layout, records[compare + 1]);
                if (operation != 2 && operation != 3) continue;
                int branch = Next(records, compare);
                if (branch >= records.Length || records[branch] != Conditional ||
                    (records[branch + 3] != 0x45 && records[branch + 3] != 0)) continue;
                if (ValueOperands.Entry(records, leftCall / Width, clauses) ||
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
                records[first] = Nop;
                records[leftCall] = Nop;
                records[second] = Nop;
                records[rightCall] = Nop;
                records[compare] = Nop;
                records[branch] = taken ? Branch : Nop;
                records[branch + 3] = 0;
                changed = true;
            }
            if (changed) RemoveUnreachable(records);
        }

        private static int Next(int[] records, int slot) => records[slot + 2] * Width;

        private static bool IsStaticMember(int[] records, int slot) =>
            slot < records.Length && records[slot] == MemberCall && records[slot + 3] == 0x28;

        private static bool GetTypeCall(byte[] image, int[] layout, int[] heaps, int[] records, int slot) =>
            IsStaticMember(records, slot) && RuntimeTypeMetadata.IsGetTypeFromHandle(image, heaps, layout, records[slot + 1]);

        private static void RemoveUnreachable(int[] records)
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
