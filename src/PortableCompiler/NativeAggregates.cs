using PeLibrary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // Stage 145 (decision 0073). Whole blittable structs move between memory 0 (Stage 143
    // layout) and the managed aggregate, which is an immutable Wasm GC struct whose null
    // reference means the default value.
    //
    // Plan table: plans[0] = used length. A plan starts with [type, size, count] followed by
    // `count` triples [ordinal offset, ldind opcode or 0, nested plan + 1 or 0], one per
    // stored ordinal of the registry layout (storage-0 ordinals are skipped, as
    // ValueFieldsWasm.Update does).
    internal sealed class NativeAggregatePlans
    {
        internal int[] Plans = new int[] { 1 };
    }

    internal static class NativeAggregates
    {
        private const int Header = 3, Entry = 3;

        // Returns the plan's index, or a negative diagnostic. The table grows by copying.
        internal static int Plan(BundleImages bundle, int[] objects, int type, NativeAggregatePlans holder, int depth = 0)
        {
            if (depth > 16 || !IsValueType(objects, type) || IsExplicitValue(objects, type)) return -2;
            int[] plans = holder.Plans;
            for (int at = 1; at < plans[0]; at += Header + plans[at + 2] * Entry)
                if (plans[at] == type) return at;

            int owner = AssemblyOf(objects, type), definition = TypeDef(objects, type);
            var measured = new int[2];
            int status = NativeValueLayouts.Measure(bundle, owner, definition, measured);
            if (status < 0) return status;

            int fields = FieldCount(objects, type), stored = 0;
            for (int i = 0; i < fields; i++) if (FieldTypeAt(objects, type, i) != 0) stored++;
            var entries = new int[stored * Entry];
            int first = FirstRow(objects, type), ordinal = 0, declared = DeclaredFieldCount(objects, type);
            var field = new int[2];
            // Registry storage uses the declared row index, including gaps
            // for static fields. The Wasm aggregate omits those gaps.
            for (int index = 0; index < declared; index++)
            {
                int storage = FieldTypeAt(objects, type, index);
                if (storage == 0) continue;
                status = NativeValueLayouts.Field(bundle, owner, definition, first + index, field);
                if (status < 0) return status;
                int nested = 0;
                if (field[1] == 0)
                {
                    nested = Plan(bundle, objects, storage, holder, depth + 1);
                    if (nested < 0) return nested;
                    nested++;
                }
                int slot = ordinal * Entry;
                entries[slot] = field[0];
                entries[slot + 1] = field[1];
                entries[slot + 2] = nested;
                ordinal++;
            }
            if (ordinal != stored) return -2;

            plans = holder.Plans;
            int start = plans[0], need = start + Header + entries.Length;
            if (need > plans.Length)
            {
                var grown = new int[need * 2];
                for (int i = 0; i < plans[0]; i++) grown[i] = plans[i];
                plans = grown;
                holder.Plans = plans;
            }
            plans[start] = type;
            plans[start + 1] = measured[0];
            plans[start + 2] = stored;
            for (int i = 0; i < entries.Length; i++) plans[start + Header + i] = entries[i];
            plans[0] = need;
            return start;
        }

        internal static int Size(int[] plans, int plan) => plans[plan + 1];

        // [address] -> [S]: one load per stored ordinal, nested structs built first.
        internal static int Load(byte[] code, int used, int[] plans, int plan, int address, int offset)
        {
            int count = plans[plan + 2];
            for (int i = 0; i < count; i++)
            {
                int at = plan + Header + i * Entry;
                int fieldOffset = offset + plans[at];
                if (plans[at + 2] != 0)
                    used = Load(code, used, plans, plans[at + 2] - 1, address, fieldOffset);
                else
                {
                    used = BranchWasm.Local(code, used, 0x20, address);
                    used = LinearMemoryWasm.Load(code, used, plans[at + 1], fieldOffset);
                }
            }
            return BranchWasm.Gc(code, used, 0, plans[plan]); // struct.new
        }

        // Stores the aggregate in `value` (already materialized) at address + offset. A nested
        // aggregate uses its own type's spill local at the store's depth: that slot holds the
        // outer struct, so the nested type's bank there is free.
        internal static int Store(byte[] code, int used, int[] plans, int[] objects, int plan, int address, int offset,
            int value, int spills, int maximum, int spillFlags, int depth)
        {
            int count = plans[plan + 2], type = plans[plan];
            for (int i = 0; i < count; i++)
            {
                int at = plan + Header + i * Entry;
                int fieldOffset = offset + plans[at];
                int storage = StoredStorage(objects, type, i);
                if (plans[at + 2] != 0)
                {
                    int nestedPlan = plans[at + 2] - 1, nestedType = plans[nestedPlan];
                    int local = BranchWasm.Spill(spills, maximum, spillFlags, nestedType, depth);
                    used = BranchWasm.Local(code, used, 0x20, value);
                    used = BranchWasm.Gc(code, used, FieldAccess.LoadInstruction(storage), type);
                    used = WasmBinary.Unsigned(code, used, i);
                    used = BranchWasm.Local(code, used, 0x21, local);
                    used = ValueFieldsWasm.Materialize(code, used, nestedType, local);
                    used = BranchWasm.Local(code, used, 0x21, local);
                    used = Store(code, used, plans, objects, nestedPlan, address, fieldOffset, local, spills, maximum, spillFlags, depth);
                }
                else
                {
                    used = BranchWasm.Local(code, used, 0x20, address);
                    used = BranchWasm.Local(code, used, 0x20, value);
                    used = BranchWasm.Gc(code, used, FieldAccess.LoadInstruction(storage), type);
                    used = WasmBinary.Unsigned(code, used, i);
                    used = LinearMemoryWasm.Store(code, used, StoreFor(plans[at + 1]), fieldOffset);
                }
            }
            return used;
        }

        // initobj S at a native address: zero sizeof(S) bytes.
        internal static int Clear(byte[] code, int used, int[] plans, int plan, int address)
        {
            used = BranchWasm.Local(code, used, 0x20, address);
            used = BranchWasm.Const(code, used, 0);
            used = BranchWasm.Const(code, used, plans[plan + 1]);
            code[used++] = 0xfc; code[used++] = 0x0b; code[used++] = 0; // memory.fill 0
            return used;
        }

        private static int StoredStorage(int[] objects, int type, int stored)
        {
            int seen = 0;
            for (int i = 0; i < FieldCount(objects, type); i++)
            {
                int storage = FieldTypeAt(objects, type, i);
                if (storage == 0) continue;
                if (seen == stored) return storage;
                seen++;
            }
            return 0;
        }

        private static int StoreFor(int load) =>
            load <= 0x47 ? 0x52 : load <= 0x49 ? 0x53 : load <= 0x4b ? 0x54 : load == 0x4c ? 0x55 :
            load == 0x4e ? 0x56 : load == 0x4f ? 0x57 : 0xdf;
    }
}
