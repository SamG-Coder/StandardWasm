using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A closed interface Type is the same canonical descriptor whether an IL
    // token or the runtime factory first required it. The factory reads an
    // ordinary descriptor array and never replaces the original managed comparer.
    internal sealed class RuntimeGenericTypes
    {
        private const int Owner = 0, Row = 1, Vector = 2, Function = 3, Width = 4;
        private const int VoidArgument = -7;
        private int[] entries = new int[4 * Width];
        private int count;
        internal bool HasFactories => count != 0;

        internal static int OpenDefinition(BundleImages bundle, int[] objects, int[] records, int slot, int[] clauses, int[] literalArgument)
        {
            // Only accept an inline length-one Type[] with index zero and an
            // exact Type producer. Every step has exactly one fall-through inlet.
            int store = RuntimeTypeRelations.Previous(records, slot, clauses);
            if (store < 0 || records[store] != ArraySet || records[store + 1] != 0xa2 || records[store + 3] != 0) return -1;
            int value = RuntimeTypeRelations.Previous(records, store, clauses);
            if (value < 0) return -1;
            if (records[value] == TypeFromHandle)
            {
                value = RuntimeTypeRelations.Previous(records, value, clauses);
                if (value < 0 || records[value] != LoadTypeToken) return -1;
                literalArgument[0] = records[value + 1];
            }
            else if (records[value] != Load) return -1;
            int index = RuntimeTypeRelations.Previous(records, value, clauses);
            if (index < 0 || records[index] != Constant || records[index + 1] != 0) return -1;
            int duplicate = RuntimeTypeRelations.Previous(records, index, clauses);
            if (duplicate < 0 || records[duplicate] != Dup) return -1;
            int vector = RuntimeTypeRelations.Previous(records, duplicate, clauses);
            if (vector < 0 || records[vector] != NewArray ||
                !RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, ArrayElementKey(objects, records[vector + 1])))
                return -1;
            int length = RuntimeTypeRelations.Previous(records, vector, clauses);
            if (length < 0 || records[length] != Constant || records[length + 1] != 1) return -1;
            int conversion = RuntimeTypeRelations.Previous(records, length, clauses);
            if (conversion < 0 || records[conversion] != TypeFromHandle) return -1;
            int receiver = RuntimeTypeRelations.Previous(records, conversion, clauses);
            return receiver >= 0 && records[receiver] == LoadTypeToken ? records[receiver + 1] : -1;
        }

        internal int Bind(BundleImages bundle, int[] objects, int[] globals, int open, int literalArgument,
            int[] owners, int[] arities, int[] offsets, int[] signatures, int used, int[] result)
        {
            int globalOwner = StaticFields.Owner(globals, open);
            int owner = RuntimeTypeTokens.ModuleSlot(globalOwner) - 1;
            int row = StaticFields.Row(globals, open);
            if (!RuntimeTypeTokens.IsDescriptorOwner(globalOwner) || owner < 0 || owner >= bundle.Count ||
                row < 2 || row >= RuntimeTypeTokens.ClosedTypeKey) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var shape = new int[5];
            int status = ReadShape(image, heaps, layout, row, shape);
            if (status < 0) return status;
            if (status != 0 || (shape[4] & 0x20) == 0) return -2;

            // Accept one unconstrained parameter. A value and a reference
            // argument rule out both nontrivial special-constraint flags;
            // a deferred nominal constraint requires a different factory.
            var deferred = new int[8];
            if (GenericParameters.Check(image, heaps, layout, row, new[] { GenericScalar.Key(8) }, objects, deferred) != 0 ||
                GenericParameters.Check(image, heaps, layout, row, new int[] { ObjectReference }, objects, deferred) != 0)
                return -2;
            if (literalArgument >= 0)
            {
                var core = CoreTypes(bundle);
                int argument = Argument(bundle, objects, globals, literalArgument, core, new int[0], prepareLiteral: true);
                if (argument != VoidArgument && argument <= 0) return argument == 0 ? -2 : argument;
            }
            for (int entry = 0; entry < count * Width; entry += Width)
                if (entries[entry + Owner] == owner && entries[entry + Row] == row)
                {
                    result[0] = entries[entry + Function];
                    return used;
                }
            int type = RegisterArray(objects, RuntimeTypeMetadata.Type);
            if (type < 0) return type;
            int offset = MethodCompiler.NextSignature(offsets, arities, used);
            if (used >= owners.Length || signatures.Length - offset < 3) return -3;
            if (count * Width == entries.Length)
            {
                var grown = new int[entries.Length * 2];
                for (int i = 0; i < entries.Length; i++) grown[i] = entries[i];
                entries = grown;
            }
            int slot = count++ * Width;
            entries[slot + Owner] = owner;
            entries[slot + Row] = row;
            entries[slot + Vector] = type;
            entries[slot + Function] = used;
            arities[used] = 2;
            offsets[used] = offset;
            signatures[offset] = RuntimeTypeMetadata.Type;
            signatures[offset + 1] = RuntimeTypeMetadata.Type;
            signatures[offset + 2] = type;
            result[0] = used;
            return used + 1;
        }

        internal int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals,
            int[] offsets, int[] sizes)
        {
            if (count == 0) return used;
            int sourceCount = globals[0]; // The pre-existing reachable descriptor set.
            var core = CoreTypes(bundle);
            var layout = new int[4];
            var heaps = new int[8];
            var arguments = new int[1];
            var noArguments = new int[0];
            for (int entry = 0; entry < count * Width; entry += Width)
            {
                int owner = entries[entry + Owner], row = entries[entry + Row];
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int function = entries[entry + Function];
                offsets[function] = used;
                if (code.Length - used < 40) return -3;
                code[used++] = 1; // one eqref local holds the argument Type.
                code[used++] = 1;
                code[used++] = 0x6d;
                used = Local(code, used, 0x20, 1);
                used = Const(code, used, 0);
                code[used++] = 0xfb;
                code[used++] = 0x0b; // array.get
                used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(entries[entry + Vector]));
                used = Local(code, used, 0x21, 2);
                used = GuardNull(code, used, 2, ExceptionTagsWasm.ArgumentNullTag);
                for (int global = 0; global < sourceCount; global++)
                {
                    int argument = Argument(bundle, objects, globals, global, core, noArguments);
                    if (argument == 0) continue;
                    bool isVoid = argument == VoidArgument;
                    if (!isVoid && argument < 0) return argument;
                    int result = -1;
                    if (!isVoid)
                    {
                        arguments[0] = argument;
                        int type = BundleLayouts.RegisterInstantiation(bundle, image, layout, heaps, owner,
                            row << 2, objects, arguments, false);
                        if (type < 0) return type;
                        result = RuntimeTypeTokens.BindLayout(bundle, objects, type, globals);
                        if (result < 0) return result;
                    }
                    if (code.Length - used < 24) return -3;
                    used = Local(code, used, 0x20, 2);
                    used = LoadGlobal(code, used, global);
                    code[used++] = 0xd3; // ref.eq
                    code[used++] = 0x04;
                    code[used++] = 0x40;
                    if (isVoid)
                        used = ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.ArgumentTag);
                    else
                    {
                        used = LoadGlobal(code, used, result);
                        code[used++] = 0x0f;
                    }
                    code[used++] = 0x0b;
                }
                code[used++] = 0x00; // No linked canonical descriptor for this argument.
                code[used++] = 0x0b;
                sizes[function] = used - offsets[function];
            }
            return used;
        }

        private static int[] CoreTypes(BundleImages bundle)
        {
            var identity = new int[2];
            var core = new int[3]; // owner, String TypeDef, Void TypeDef
            core[0] = -1;
            if (RuntimeTypeTokens.FindCoreType(bundle, "String", identity) == 0)
            {
                core[0] = identity[0];
                core[1] = identity[1];
            }
            if (RuntimeTypeTokens.FindCoreType(bundle, "Void", identity) == 0 && identity[0] == core[0])
                core[2] = identity[1];
            return core;
        }

        private static int Argument(BundleImages bundle, int[] objects, int[] globals, int global,
            int[] core, int[] noArguments, bool prepareLiteral = false)
        {
            int key = StaticFields.Owner(globals, global), row = StaticFields.Row(globals, global);
            if (!RuntimeTypeTokens.IsDescriptorOwner(key) || row < 0) return 0;
            int owner = RuntimeTypeTokens.ModuleSlot(key) - 1;
            if (owner < 0)
            {
                if (row == 0) return ObjectReference;
                if (row < RuntimeTypeTokens.PrimitiveVectorKey) return 0;
                int primitive = GenericScalar.Key(row - RuntimeTypeTokens.PrimitiveVectorKey);
                return primitive == 0 ? 0 : GenericScalar.VectorKey(primitive);
            }
            if (owner >= bundle.Count || row >= RuntimeTypeTokens.PrimitiveVectorKey) return 0;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                return type >= FirstClass && type - FirstClass < objects[0] && !IsByRefLikeValue(objects, type) ? type : 0;
            }
            int[] layout = null, heaps = null;
            byte[] image = null;
            if (owner == core[0] || prepareLiteral)
            {
                image = bundle.Get(owner);
                layout = new int[4];
                heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            }
            if (owner == core[0])
            {
                if (row == core[1]) return String;
                if (row == core[2]) return VoidArgument;
                // The CLI key must win even when a linked method independently
                // reserved a nominal layout for this same CoreLib primitive.
                int element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, row << 2);
                if (element > 0) return GenericScalar.Key(element);
            }
            int known = Find(objects, owner, row, noArguments);
            if (known != 0) return IsByRefLikeValue(objects, known) ? 0 : known;
            int enumeration = FindEnum(objects, owner, row);
            if (enumeration != 0 || !prepareLiteral) return enumeration;

            // A literal descriptor can be the only managed use of this TypeDef.
            // Register its real layout now so failures retain the calling IL location.
            var deferred = new int[8];
            int status = GenericParameters.Check(image, heaps, layout, row, noArguments, objects, deferred);
            if (status < 0) return status;
            var shape = new int[5];
            status = ReadShape(image, heaps, layout, row, shape);
            bool value = status == -2;
            if (value) status = ReadShape(image, heaps, layout, row, shape, true);
            if (status < 0) return status;
            int argument = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, row << 2, objects, value, true);
            return IsByRefLikeValue(objects, argument) ? -2 : argument;
        }

        private static int LoadGlobal(byte[] code, int used, int global)
        {
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, global);
        }
    }
}
