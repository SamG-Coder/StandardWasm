using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;
using static PortableCompiler.ValueFieldProjections;

namespace PortableCompiler
{
    internal static class ValueFieldProjectionWasm
    {
        internal const int FirstSnapshotHelper = 4, FirstBooleanHelper = 6, FirstWideHelper = 8, HelperCount = 10;

        internal static bool IsSnapshotHelper(int helper) => helper >= FirstSnapshotHelper && helper < FirstBooleanHelper;

        internal static int Take(byte[] code, int used, int source, int owner, int selector, int location, bool valueOwner, bool tracksRoots)
        {
            // The pair retains the containing object or the stable value cell.
            used = GuardNull(code, used, source);
            used = valueOwner ? LoadOperand(code, used, source, ValueLocation(owner), ValueLocation(owner)) :
                Local(code, used, 0x20, source);
            // Array indices and value-cell selectors are nonnegative. Direct
            // class-field selectors carry the original class as their owner.
            used = Const(code, used, valueOwner ? selector : ~selector);
            used = Gc(code, used, 0, ObjectLocation);
            // A ValueLocation normally holds a nominal snapshot. An
            // ObjectLocation payload instead describes a projected field.
            // The view group starts with a non-final native array. The nominal
            // group emits only final arrays, so its snapshots have distinct heaps.
            return IsValueLocation(location) ? ManagedValueLocationWasm.CreateCell(code, used, tracksRoots) : used;
        }

        internal static int Read(byte[] code, int used, int start, int address, int vector, int location,
            ValueFieldProjections projections)
        {
            if (projections.Uses(location))
            {
                used = Local(code, used, 0x20, address);
                return Call(code, used, start, location, false, projections);
            }
            used = ManagedLocationWasm.AccessElement(code, used, address, vector);
            return Gc(code, used, vector == Bytes || vector == Chars ? 0x0d : 0x0b, vector);
        }

        internal static int Write(byte[] code, int used, int start, int address, int source, int vector, int location,
            ValueFieldProjections projections)
        {
            bool dispatch = projections.Uses(location);
            used = dispatch ? Local(code, used, 0x20, address) :
                ManagedLocationWasm.AccessElement(code, used, address, vector);
            if (source >= 0) used = Local(code, used, 0x20, source);
            else if (vector == Bytes) used = Const(code, used, 0);
            else if (vector == Longs) used = WideConst(code, used, 0, 0);
            else
            {
                code[used++] = 0xd0; // ref.null eq
                code[used++] = 0x6d;
            }
            return dispatch ? Call(code, used, start, location, true, projections) : Gc(code, used, 0x0e, vector);
        }

        private static int Call(byte[] code, int used, int start, int location, bool write, ValueFieldProjections projections)
        {
            int helper = location == Int32Location ? 0 : location == BooleanLocation ? FirstBooleanHelper :
                location == Int64Location ? FirstWideHelper : 2;
            if (write) helper++;
            return CallHelper(code, used, start, helper, projections);
        }

        internal static int CallSnapshot(byte[] code, int used, int start, bool write, ValueFieldProjections projections) =>
            CallHelper(code, used, start, FirstSnapshotHelper + (write ? 1 : 0), projections);

        private static int CallHelper(byte[] code, int used, int start, int helper, ValueFieldProjections projections)
        {
            code[used++] = 0x10;
            return WasmIndexFixups.WriteAt(code, used, start, projections.Fixups, helper);
        }

        internal static int ValueType(int helper) => helper >= FirstWideHelper ? I64 :
            helper < 2 || helper >= FirstBooleanHelper ? I32 : ObjectReference;

        internal static int WriteHelper(byte[] code, int used, int[] objects, ValueFieldProjections projections, int helper)
        {
            bool write = (helper & 1) != 0;
            bool aggregate = IsSnapshotHelper(helper);
            int kind = aggregate ? Aggregate : helper < 2 ? Scalar :
                helper >= FirstWideHelper ? WideScalar : helper >= FirstBooleanHelper ? BooleanScalar : Reference;
            int value = ValueType(helper);
            int vector = kind == Scalar ? Ints : kind == BooleanScalar ? Bytes :
                kind == WideScalar ? Longs : projections.ReferenceVector;
            int scalarLocation = kind == Scalar ? Int32Location : kind == WideScalar ? Int64Location : 0;
            const int address = 0, replacement = 1;
            int parent = write ? 2 : 1;
            int descriptor = aggregate ? parent + 1 : address;
            int locals = aggregate ? 2 : 1;
            for (int i = 0; i < projections.Count; i++)
                if (ValueFieldProjections.Kind(projections.Location(i)) == kind &&
                    IsValueType(objects, projections.Owner(i))) locals++;
            if (code.Length - used < 96 || locals > 49998 || locals > (code.Length - used - 96) / 8) return -3;
            used = WasmBinary.Unsigned(code, used, locals);
            code[used++] = 1;
            used = WasmBinary.ValueType(code, used, ValueLocation(FirstClass));
            if (aggregate)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, ObjectLocation);
            }
            for (int i = 0; i < projections.Count; i++)
            {
                if (ValueFieldProjections.Kind(projections.Location(i)) != kind ||
                    !IsValueType(objects, projections.Owner(i))) continue;
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, projections.Owner(i));
            }

            used = GuardNull(code, used, address);
            if (scalarLocation != 0 && projections.UsesClass(scalarLocation))
            {
                used = ClassScalars(code, used, objects, projections, address, replacement, write, scalarLocation);
                if (used < 0) return used;
            }
            used = ProjectionMarker(code, used, address, aggregate);
            int locationType = aggregate ? ObjectLocation : ValueLocation(FirstClass);
            used = RefType(code, used, 0x14, locationType); // ref.test, non-null
            code[used++] = 0x04; code[used++] = 0x40;
            if (aggregate)
            {
                used = ProjectionMarker(code, used, address, true);
                used = RefType(code, used, 0x16, ObjectLocation);
                used = Local(code, used, 0x21, descriptor);
            }
            used = ManagedLocationWasm.LoadField(code, used, descriptor, 0);
            used = RefType(code, used, 0x16, ValueLocation(FirstClass)); // ref.cast, non-null
            used = Local(code, used, 0x21, parent);
            int snapshot = parent + (aggregate ? 2 : 1);
            for (int i = 0; i < projections.Count; i++)
            {
                int location = projections.Location(i);
                if (ValueFieldProjections.Kind(location) != kind) continue;
                int owner = projections.Owner(i), field = projections.Ordinal(i);
                if (!IsValueType(objects, owner)) continue;
                int fields = write ? FieldCount(objects, owner) : 0;
                if (code.Length - used < 128 || fields > (code.Length - used - 128) / 16) return -3;
                used = ManagedLocationWasm.LoadField(code, used, descriptor, 1);
                used = Const(code, used, i);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40;
                if (write)
                    used = ValueFieldsWasm.Update(code, used, objects, owner, parent, snapshot, field,
                        replacement, value, location: true, widenedReplacement: value == ObjectReference, projections: projections);
                else
                {
                    used = ManagedValueLocationWasm.ReadLocation(code, used, parent, owner, projections);
                    used = Local(code, used, 0x21, snapshot);
                    used = ValueFieldsWasm.Materialize(code, used, owner, snapshot);
                    // The ordinal is physical. Boolean projections have already
                    // proved packed storage and require struct.get_u.
                    used = Gc(code, used, kind == BooleanScalar ? 4 : 2, owner);
                    used = WasmBinary.Unsigned(code, used, field);
                }
                code[used++] = 0x0f; // return
                code[used++] = 0x0b;
                snapshot++;
            }
            if (code.Length - used < 48) return -3;
            code[used++] = 0x00; // The owning cell always carries a selector for this location kind.
            code[used++] = 0x0b;
            if (aggregate)
            {
                used = Local(code, used, 0x20, address);
                if (write)
                {
                    used = Local(code, used, 0x20, replacement);
                    used = ManagedValueLocationWasm.WriteSnapshot(code, used);
                }
                else used = ManagedValueLocationWasm.ReadRawSnapshot(code, used);
            }
            else
            {
                used = ManagedLocationWasm.AccessElement(code, used, address, vector);
                if (write) used = Local(code, used, 0x20, replacement);
                used = Gc(code, used, write ? 0x0e : vector == Bytes ? 0x0d : 0x0b, vector);
            }
            code[used++] = 0x0b;
            return used;
        }

        private static int ClassScalars(byte[] code, int used, int[] objects, ValueFieldProjections projections,
            int address, int replacement, bool write, int location)
        {
            if (code.Length - used < 96) return -3;
            used = ManagedLocationWasm.LoadField(code, used, address, 1);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            code[used++] = 0x04; code[used++] = 0x40;
            for (int i = 0; i < projections.Count; i++)
            {
                int owner = projections.Owner(i);
                if (projections.Location(i) != location || IsValueType(objects, owner)) continue;
                if (code.Length - used < 96) return -3;
                used = ManagedLocationWasm.LoadField(code, used, address, 1);
                used = Const(code, used, ~i);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40;
                used = ManagedLocationWasm.LoadField(code, used, address, 0);
                used = RefType(code, used, 0x16, owner);
                if (write) used = Local(code, used, 0x20, replacement);
                used = Gc(code, used, write ? 5 : 2, owner);
                used = WasmBinary.Unsigned(code, used, projections.Ordinal(i));
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            if (code.Length - used < 2) return -3;
            code[used++] = 0x00; // Negative selectors belong to a registered class field.
            code[used++] = 0x0b;
            return used;
        }

        private static int ProjectionMarker(byte[] code, int used, int address, bool aggregate) =>
            aggregate ? ManagedValueLocationWasm.ReadRawSnapshot(code, Local(code, used, 0x20, address)) :
                ManagedLocationWasm.LoadField(code, used, address, 0);

        private static int RefType(byte[] code, int used, byte opcode, int type)
        {
            code[used++] = 0xfb;
            code[used++] = opcode;
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
        }
    }
}
