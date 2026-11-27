using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class BoxNullTests
    {
        // A non-nullable value cannot produce a null box. This pattern
        // observes only nullness, so it needs neither a box nor a reference layout.
        internal static bool TryFoldBoxNullTest(int[] records, int slot, int[] clauses)
        {
            int literal = records[slot + 2] * Width;
            if (literal >= records.Length || records[literal] != NullLiteral ||
                ValueOperands.Entry(records, literal / Width, clauses)) return false;
            int compare = records[literal + 2] * Width;
            if (compare >= records.Length || records[compare] != Binary ||
                ValueOperands.Entry(records, compare / Width, clauses)) return false;
            int operation = records[compare + 1];
            if (operation != 0x46 && operation != 0x4b) return false;

            // Keep the original value evaluation, including its possible exceptions.
            records[slot] = Pop;
            records[literal] = Nop;
            records[compare] = Constant;
            records[compare + 1] = operation == 0x46 ? 0 : 1;
            records[compare + 3] = 0;
            return true;
        }
    }
}
