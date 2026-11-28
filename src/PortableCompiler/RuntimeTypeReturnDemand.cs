using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class RuntimeTypeReturnDemand
    {
        // Find the producer at a descriptor comparison along a fallthrough
        // stack segment. A field load is encoded as -2 minus its record slot.
        // A local must have one direct-call definition, apart from null writes.
        internal static int Find(int[] records, int pc, int index, int[] depths, int[] arities,
            int[] frame, int[] clauses, int type, int[] signatures, int[] offsets)
        {
            int at = pc * Width;
            while (at != 0)
            {
                if (ValueOperands.Entry(records, at / Width, clauses)) return -1;
                int previous = Predecessor(records, at);
                if (previous < 0 || depths[previous / Width] == 0) return -1;
                int before = depths[previous / Width] - 1;
                int kind = records[previous], operand = records[previous + 1];
                if (kind == Call)
                {
                    int start = before - arities[operand];
                    if (index >= start)
                        return index == start && signatures[offsets[operand]] == type ? operand : -1;
                }
                else if (kind == GetField)
                {
                    if (index >= before - 1)
                        return index == before - 1 && FieldAccess.Storage(records[previous + 3]) == type ? -2 - previous : -1;
                }
                else if (kind == Load)
                {
                    if (index == before)
                        return frame[operand] == type ? LocalSource(records, operand, clauses, type, signatures, offsets) : -1;
                    if (index > before) return -1;
                }
                else if (kind == LoadTypeToken || kind == NullLiteral || kind == LoadString || kind == Constant)
                {
                    if (index >= before) return -1;
                }
                else if (kind == TypeFromHandle)
                {
                    if (index >= before - 1) return -1;
                }
                else if (kind == Dup)
                {
                    if (before == 0 || index > before) return -1;
                    if (index == before) index = before - 1;
                }
                else if (kind != Nop)
                    return -1;
                at = previous;
            }
            return -1;
        }

        private static int LocalSource(int[] records, int local, int[] clauses, int type, int[] signatures, int[] offsets)
        {
            int source = -1;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (records[slot + 1] != local) continue;
                if (kind == FrameAddress || kind == LoadFrameLocation || kind == ClearValue) return -1;
                if (kind != Store) continue;
                int previous = Predecessor(records, slot);
                if (previous < 0 || ValueOperands.Entry(records, slot / Width, clauses)) return -1;
                int producer = records[previous];
                if (producer == NullLiteral || producer == TypeFromHandle || producer == GetObjectType) continue;
                if (producer != Call) return -1;
                int call = records[previous + 1];
                int result = signatures[offsets[call]];
                if (result == RuntimeTypeMetadata.Type) continue;
                if (result != type || source >= 0 && source != call) return -1;
                source = call;
            }
            return source;
        }

        private static int Predecessor(int[] records, int slot)
        {
            int previous = slot - Width;
            while (previous >= 0 && records[previous] == 0) previous -= Width;
            return previous >= 0 && records[previous + 2] * Width == slot ? previous : -1;
        }
    }
}
