using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NestedConstructorFields
    {
        // A readonly field's address can be used for this direct write to
        // the current constructor receiver. Prove that it cannot escape
        // into any other instruction or across an independently entered edge.
        internal static bool Bind(byte[] image, int[] layout, int[] heaps, int owner, int[] objects,
            int[] records, int address, int[] clauses, int outer, int child, int outerField)
        {
            if (IsExplicitValue(objects, outer) || IsByRefLikeValue(objects, outer) ||
                !IsValueType(objects, child) || IsByRefLikeValue(objects, child) ||
                AssemblyOf(objects, child) != owner || ValueOperands.Entry(records, address / Width, clauses))
                return false;

            int receiver = address - Width;
            while (receiver >= 0 && records[receiver] == 0) receiver -= Width;
            if (receiver < 0 || records[receiver] != LoadConstructorAddress || records[receiver + 2] != address / Width)
                return false;
            int value = records[address + 2] * Width;
            if (value >= records.Length || ValueOperands.Entry(records, value / Width, clauses)) return false;
            int producer = records[value];
            if (producer != Load && producer != Constant && producer != WideConstant &&
                producer != SingleConstant && producer != DoubleConstant) return false;
            int store = records[value + 2] * Width;
            if (store >= records.Length || records[store] != SetField || records[store + 3] != 0 ||
                ValueOperands.Entry(records, store / Width, clauses)) return false;

            int field = records[store + 1];
            if (ObjectLayouts.Owner(image, heaps, layout, field, true) != TypeDef(objects, child) ||
                !ValueOperands.IsWritableInteriorField(image, layout, heaps, field, false)) return false;
            int ordinal = FieldOrdinal(objects, child, field);
            if (ordinal < 0) return false;
            int storage = FieldType(objects, child, field);
            if (!IsScalar(storage)) return false;

            // Keep the constructor address and original scalar load on the
            // stack. The store updates the nested snapshot and its parent.
            records[address] = Nop;
            records[store] = UpdateNestedConstructorField;
            records[store + 1] = ordinal;
            records[store + Width + 1] = outerField;
            records[store + 3] = FieldAccess.Encode(child, storage);
            return true;
        }

        internal static int Emit(byte[] code, int used, int[] objects, int outer, int outerField, int child,
            int childField, int storage, int replacement, int replacementType, int temporaries)
        {
            int snapshot = temporaries + child - FirstClass;
            used = ValueFieldsWasm.Materialize(code, used, outer, 0);
            used = Gc(code, used, FieldAccess.LoadInstruction(child), outer);
            used = WasmBinary.Unsigned(code, used, outerField);
            used = Local(code, used, 0x21, snapshot);

            // Reuse the exact overlay projection so a narrow write leaves
            // the child's other bytes intact before the outer value changes.
            if (UsesPackedExplicitValue(objects, child))
                used = ExplicitValueWasm.Update(code, used, objects, child, snapshot, childField, storage, replacement, replacementType);
            else if (HasExplicitSlots(objects, child))
                used = ExplicitValueWasm.UpdateSlot(code, used, objects, child, snapshot, snapshot,
                    childField, storage, replacement, replacementType);
            else
                used = ValueFieldsWasm.Update(code, used, objects, child, snapshot, snapshot,
                    childField, replacement, replacementType);
            return ValueFieldsWasm.Update(code, used, objects, outer, 0, temporaries + outer - FirstClass,
                outerField, snapshot, child);
        }
    }
}
