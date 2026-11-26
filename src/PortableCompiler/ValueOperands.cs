using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ValueOperands
    {
        internal static int LowerLocal(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] records, int slot, int[] frame, int[] objects, int[] clauses)
        {
            int local = records[slot + 1], type = frame[local];
            if (!ObjectLayouts.IsValueType(objects, type)) return -2;
            int next = records[slot + 2] * Width;
            if (next >= records.Length || Entry(records, next / Width, clauses)) return -2;
            int kind = records[next];
            if (kind == InitializeValue)
            {
                uint token = unchecked((uint)records[next + 1]);
                uint table = token >> 24;
                if (table != 1 && table != 2) return -2;
                int coded = (int)((token & 0xffffff) << 2) | (table == 1 ? 1 : 0);
                int initialized = BundleLayouts.RegisterType(main, bundle, ranges, image, layout, heaps, owner, coded, objects, true);
                if (initialized < 0 || initialized != type) return -2;
                records[slot] = ClearValue;
                records[slot + 2] = records[next + 2];
                records[next] = 0;
                return 0;
            }
            if (kind == GetField)
            {
                records[slot] = Load;
                return 0;
            }

            // A writable local address can survive only this straight-line
            // calculation. Track its temporary stack separately so it cannot
            // consume values below the original address or escape via a call.
            int depth = 0;
            while (next < records.Length)
            {
                if (Entry(records, next / Width, clauses)) return -2;
                kind = records[next];
                if (kind == SetField && depth == 1)
                {
                    records[slot] = Nop;
                    records[next + Width + 1] = local + 1;
                    return 0;
                }
                if (kind == Load || kind == Constant || kind == WideConstant || kind == SingleConstant ||
                    kind == DoubleConstant || kind == NullLiteral || kind == LoadString) depth++;
                else if (kind == Binary || kind == CheckedInteger)
                {
                    if (depth < 2) return -2;
                    depth--;
                }
                else if (kind != Nop && (depth == 0 || kind != Unary && kind != Narrow &&
                    kind != ConvertInteger && kind != ConvertFloat && kind != CheckedConversion)) return -2;
                next = records[next + 2] * Width;
            }
            return -2;
        }

        internal static bool Entry(int[] records, int target, int[] clauses)
        {
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                int tryStart = clauses[clause + 1], handler = clauses[clause + 3];
                if (target == tryStart || target == tryStart + clauses[clause + 2] ||
                    target == handler || target == handler + clauses[clause + 4]) return true;
            }
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if ((kind == Branch || kind == Conditional || kind == Leave) && records[slot + 1] == target) return true;
                if (kind == Switch)
                    for (int i = 0; i < records[slot + 3]; i++)
                        if (SwitchTarget(records, records[slot + 1], i) == target) return true;
            }
            return false;
        }
    }
}
