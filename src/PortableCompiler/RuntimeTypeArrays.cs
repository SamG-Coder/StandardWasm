using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class RuntimeTypeArrays
    {
        internal static int Specialize(BundleImages bundle, int[] records, int[] frame,
            int parameters, int locals, int[] objects, int[] clauses)
        {
            for (int allocation = 0; allocation < records.Length; allocation += Width)
            {
                if (records[allocation] != NewArray) continue;
                int declared = records[allocation + 1];
                if (!RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, ArrayElementKey(objects, declared))) continue;
                int after = ScanInitializer(records, allocation, frame, parameters + locals, clauses);
                if (after < 0) continue;
                int type = RegisterArray(objects, RuntimeTypeMetadata.Type);
                if (type < 0) return type;
                records[allocation + 1] = type;
                SpecializeLocal(records, frame, parameters, locals, declared, type, after);
            }
            return 0;
        }

        private static int ScanInitializer(int[] records, int allocation, int[] frame, int frameSize, int[] clauses)
        {
            int cursor = Next(records, clauses, allocation);
            bool descriptor = false;
            while (cursor >= 0 && records[cursor] == Dup)
            {
                int index = Next(records, clauses, cursor);
                if (index < 0 || records[index] != Constant) break;
                int value = Next(records, clauses, index);
                if (value < 0) break;
                int kind = records[value], local = records[value + 1];
                bool known = kind == Load && local >= 0 && local < frameSize && frame[local] == RuntimeTypeMetadata.Type;
                if (kind == LoadTypeToken)
                {
                    value = Next(records, clauses, value);
                    known = value >= 0 && records[value] == TypeFromHandle;
                }
                if (!known && kind != NullLiteral) break;
                int store = Next(records, clauses, value);
                if (store < 0 || records[store] != ArraySet || records[store + 1] != 0xa2 || records[store + 3] != 0) break;
                descriptor |= known;
                cursor = Next(records, clauses, store);
            }
            // The duplicated reference is the fresh array. Each proof follows a
            // single fall-through path until stelem.ref consumes that duplicate.
            return descriptor && cursor >= 0 ? cursor : -1;
        }

        private static void SpecializeLocal(int[] records, int[] frame, int parameters, int locals,
            int declared, int type, int store)
        {
            int local = records[store + 1];
            if (records[store] != Store || local < parameters || local - parameters >= locals || frame[local] != declared) return;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (slot != store && records[slot + 1] == local &&
                    (kind == Store || kind == FrameAddress || kind == LoadFrameLocation))
                    return;
            }
            // An unknown local write or address could change the representation.
            frame[local] = type;
        }

        private static int Next(int[] records, int[] clauses, int slot)
        {
            int position = records[slot + 2];
            int target = position * Width;
            return target > slot && target < records.Length && !ValueOperands.Entry(records, position, clauses) ? target : -1;
        }
    }
}
