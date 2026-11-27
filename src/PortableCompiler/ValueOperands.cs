using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ValueOperands
    {
        private const int CallConsumesAddress = -3;

        internal static int LowerFrame(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methodCount, int[] records, int slot, int[] frame, int[] objects, int[] clauses,
            int context, int[] methodArguments)
        {
            int local = records[slot + 1], type = frame[local];
            bool value = type == RuntimeTypeMetadata.Handle || SignatureTypes.IsSpan(type) || ObjectLayouts.IsValueType(objects, type);
            if (!value && !SignatureTypes.IsObjectReference(type)) return -2;
            int next = records[slot + 2] * Width;
            if (next >= records.Length || Entry(records, next / Width, clauses)) return -2;
            int kind = records[next];
            if (kind == InitializeValue)
                return Clear(bundle, image, layout, heaps, owner, records, slot, next, type, objects, context, methodArguments);
            if (!value || type == RuntimeTypeMetadata.Handle || SignatureTypes.IsSpan(type)) return -2;
            if (kind == GetField)
            {
                records[slot] = Load;
                return 0;
            }
            bool locationAvailable = !ObjectLayouts.IsExplicitValue(objects, type);
            return LowerAddressUse(image, layout, heaps, methodCount, records, slot, local, clauses, locationAvailable: locationAvailable);
        }

        internal static int BindReceiverInitialization(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] records, int address, int receiver, int[] objects, int[] clauses,
            int context, int[] methodArguments)
        {
            int next = records[address + 2] * Width;
            if (next >= records.Length || records[next] != InitializeValue || Entry(records, next / Width, clauses))
                return 0;
            int status = CheckInitialization(bundle, image, layout, heaps, owner, objects, context, methodArguments,
                records[next + 1], SignatureTypes.LocatedValue(receiver));
            if (status < 0) return status;
            // The caller and any other aliases keep the same cell. Only its
            // current snapshot changes to the CLI default value.
            records[next] = ClearManagedCell;
            records[next + 1] = 0;
            records[next + 3] = receiver;
            return 0;
        }

        // In a value-type instance constructor, ldarg.0 pushes the managed
        // address of the value under construction. The Wasm receiver
        // parameter holds that value's current immutable aggregate, and the
        // constructor returns it (decision 0042). Every use must be one of
        // the address forms below, so the address never escapes the frame.
        // In any other value-type instance method the receiver is read-only
        // (decision 0049). Its address may only feed ldfld or a call on the
        // same address, so initobj, stfld and a chained constructor stay
        // diagnosed and the callee never writes the caller's location.
        internal static int LowerReceiver(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methodCount, int[] records, int slot, int[] frame, int[] objects, int[] clauses, bool readOnly, int context, int[] methodArguments)
        {
            int next = records[slot + 2] * Width;
            if (next >= records.Length || Entry(records, next / Width, clauses)) return -2;
            int kind = records[next];
            if (kind == GetField) return 0;
            if (kind == LoadIndirect && readOnly)
            {
                int receiver = frame[0];
                if (ObjectLayouts.AssemblyOf(objects, receiver) != owner) return -2;
                int element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, ObjectLayouts.TypeDef(objects, receiver) << 2);
                if (element < 0) return element;
                int storage = PrimitiveBoxing.Storage(element);
                if (!PrimitiveLoadMatches(storage, records[next + 1]) ||
                    !PrimitiveBoxing.HasPayloadField(objects, receiver, storage)) return -2;
                // CoreLib can dereference primitive this directly (ldind).
                // Read the snapshot's verified payload field without a native address.
                records[next] = BoundScopedFieldLoad;
                records[next + 1] = 0;
                records[next + 3] = FieldAccess.Encode(receiver, storage);
                return 0;
            }
            if (kind == InitializeValue)
                return readOnly ? -2 :
                    Clear(bundle, image, layout, heaps, owner, records, slot, next, frame[0], objects, context, methodArguments);
            return LowerAddressUse(image, layout, heaps, methodCount, records, slot, 0, clauses, readOnly);
        }

        internal static bool PrimitiveLoadMatches(int storage, int opcode)
        {
            if (storage == I32) return opcode == 0x4a || opcode == 0x4b;
            if (storage == I64) return opcode == 0x4c;
            if (storage == Native) return opcode == 0x4d;
            if (storage == F32) return opcode == 0x4e;
            if (storage == F64) return opcode == 0x4f;
            if (storage == Signed8) return opcode == 0x46;
            if (storage == Unsigned8) return opcode == 0x47;
            if (storage == Signed16) return opcode == 0x48;
            return (storage == Unsigned16 || storage == Character) && opcode == 0x49;
        }

        // Keep the owning value location on the stack for an immediate read
        // or an arithmetic read-modify-write of a scoped Int32 field.
        internal static bool LowerScopedIntField(int[] records, int address, int[] clauses)
        {
            int load = records[address + 2] * Width;
            if (load >= records.Length || Entry(records, load / Width, clauses)) return false;
            bool modifies = records[load] == Dup;
            if (modifies) load = records[load + 2] * Width;
            if (load >= records.Length || Entry(records, load / Width, clauses) ||
                records[load] != LoadIndirect || records[load + 1] != 0x4a) return false;
            int store = -1;
            if (modifies)
            {
                int depth = 1;
                for (int at = records[load + 2] * Width; at < records.Length; at = records[at + 2] * Width)
                {
                    if (Entry(records, at / Width, clauses)) return false;
                    int kind = records[at];
                    if (kind == StoreIndirect && records[at + 1] == 0x54 && depth == 1)
                    {
                        store = at;
                        break;
                    }
                    if (kind == Load || kind == Constant) depth++;
                    else if ((kind == Binary || kind == CheckedInteger) && depth >= 2) depth--;
                    else return false;
                }
                if (store < 0) return false;
            }
            BindScopedField(records, load, address, BoundScopedFieldLoad);
            if (store >= 0) BindScopedField(records, store, address, BoundScopedFieldStore);
            records[address] = Nop;
            return true;
        }

        private static void BindScopedField(int[] records, int at, int address, int kind)
        {
            records[at] = kind;
            records[at + 1] = records[address + 1];
            records[at + 3] = records[address + 3];
        }

        internal static int LowerClassField(byte[] image, int[] layout, int[] heaps, int methodCount,
            int[] records, int slot, int storage, int[] clauses) =>
            LowerAddressUse(image, layout, heaps, methodCount, records, slot, 0, clauses, fieldReceiver: storage);

        internal static bool IsWritableInteriorField(byte[] image, int[] layout, int[] heaps, int row, bool constructorReceiver)
        {
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return false;
            var definition = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, definition);
            return (definition[0] & 0x20) == 0 || constructorReceiver;
        }

        // A frame address followed by initobj of the same type clears the location.
        private static int Clear(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] records, int slot, int next, int type, int[] objects, int context, int[] methodArguments)
        {
            int status = CheckInitialization(bundle, image, layout, heaps, owner, objects,
                context, methodArguments, records[next + 1], type);
            if (status < 0) return status;
            // The operand stays the original frame index.
            records[slot] = ClearValue;
            records[slot + 2] = records[next + 2];
            records[next] = 0;
            return 0;
        }

        internal static int CheckInitialization(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] objects, int context, int[] methodArguments, int operand, int type)
        {
            uint token = unchecked((uint)operand);
            uint table = token >> 24;
            int row = (int)(token & 0xffffff);
            int initialized;
            if (table == 0x1b)
            {
                int identity = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, row, context, objects, methodArguments);
                if (identity < 0) return identity;
                initialized = ObjectLayouts.SignatureStorage(objects, identity);
            }
            else
            {
                if (table != 1 && table != 2) return -2;
                int coded = row << 2 | (table == 1 ? 1 : 0);
                initialized = RuntimeTypeMetadata.Storage(image, heaps, layout, coded, true);
                if (initialized == 0)
                {
                    if (SignatureTypes.IsObjectReference(type) && !ObjectLayouts.IsValueType(objects, type))
                        initialized = PlatformTypeReferences.IsString(image, heaps, layout, coded) ? SignatureTypes.String :
                            BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
                    else
                        initialized = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, true);
                }
            }
            return initialized == type ? 0 : -2;
        }

        // A writable address can survive only a straight-line calculation
        // that ends in stfld or in a call on that address. Track the
        // temporary stack separately so the sequence cannot consume values
        // below the original address or pass it anywhere else.
        private static int LowerAddressUse(byte[] image, int[] layout, int[] heaps, int methodCount,
            int[] records, int slot, int local, int[] clauses, bool readOnly = false, int fieldReceiver = 0, bool locationAvailable = false)
        {
            int next = records[slot + 2] * Width;
            int depth = 0;
            while (next < records.Length)
            {
                if (Entry(records, next / Width, clauses)) return -2;
                int kind = records[next];
                if (kind == SetField && depth == 1)
                {
                    if (readOnly || fieldReceiver != 0) return -2;
                    // [&value, field] -> stfld -> []. UpdateValue replaces the local's aggregate.
                    records[slot] = Nop;
                    records[next + Width + 1] = local + 1;
                    return 0;
                }
                bool construction = kind == NewObject || kind == MemberCall && records[next + 3] == 0x73;
                if (construction || kind == Call && records[next + 3] == 0 || kind == MemberCall && records[next + 3] == 0x28)
                {
                    int after = TemporaryCallDepth(image, layout, heaps, methodCount,
                        kind, records[next + 1], depth, construction);
                    if (after >= 0)
                    {
                        // The call or construction consumes only temporary operands.
                        // The managed address stays pending on the stack.
                        depth = after;
                        next = records[next + 2] * Width;
                        continue;
                    }

                    // A static call that consumes the address uses a real managed location.
                    if (after == CallConsumesAddress || construction) return -2;
                    // An ordinary instance method can mutate or pass on this address.
                    // Use the frame's shared location rather than its current snapshot.
                    if (locationAvailable && !IsConstructorCall(image, layout, heaps, kind, records[next + 1])) return -2;
                    // [&value, arguments] -> call -> []. The link step accepts only
                    // a value-type instance constructor (ConstructValue) or a
                    // read-only instance method (decision 0049) with exactly
                    // these arguments. The receiver is pushed as the
                    // current value, and the call's spare operand records keep
                    // the location and the argument count.
                    if (records[slot] == FrameAddress) records[slot] = Load;
                    records[next + Width + 1] = fieldReceiver == 0 ? local + 1 : -fieldReceiver;
                    records[next + 2 * Width + 1] = depth + 1;
                    return 0;
                }
                if (kind == Load || kind == LoadStaticField || kind == Constant || kind == WideConstant || kind == SingleConstant ||
                    kind == DoubleConstant || kind == NullLiteral || kind == LoadString || kind == SizeOf) depth++;
                else if (kind == Binary || kind == CheckedInteger)
                {
                    if (depth < 2) return -2;
                    depth--;
                }
                else if (kind == FieldAddress && depth > 0)
                {
                    // A nested field read can use a temporary receiver above
                    // the pending destination. Its address must be consumed
                    // immediately so no call can observe or retain it.
                    int read = records[next + 2] * Width;
                    if (read >= records.Length || records[read] != GetField ||
                        Entry(records, read / Width, clauses)) return -2;
                }
                // A field read of an operand above the address (for example
                // this.X = this.Y) observes the location's current contents.
                else if (kind != Nop && (depth == 0 || kind != Unary && kind != Narrow && kind != GetField &&
                    kind != ConvertInteger && kind != ConvertFloat && kind != CheckedConversion)) return -2;
                next = records[next + 2] * Width;
            }
            return -2;
        }

        private static bool IsConstructorCall(byte[] image, int[] layout, int[] heaps, int kind, int row)
        {
            if (kind == Call) return ObjectLayouts.IsConstructor(image, heaps, layout, row);
            var reference = new int[AssemblyReferences.SymbolWidth];
            return AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) >= 0 &&
                ImageBytes.MatchesAscii(image, heaps, (uint)reference[4], ".ctor");
        }

        // Read only the stack effect. Normal call binding still validates the
        // signature, the target and the types of every argument and result.
        private static int TemporaryCallDepth(byte[] image, int[] layout, int[] heaps, int methodCount,
            int kind, int row, int depth, bool construction)
        {
            int blob;
            if (kind == Call || kind == NewObject)
            {
                if (row < 1 || row > methodCount ||
                    ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) != 0) == construction) return -1;
                blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0)
                    return -1;
                blob = reference[5];
            }

            if (blob < 1 || blob >= heaps[1]) return -1;
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            // Default calling convention. A constructor has a receiver in its signature,
            // but newobj supplies it and consumes only the explicit arguments.
            if (size < 3 || size > limit - start || image[start] != (construction ? 0x20 : 0)) return -1;
            int end = start + size;
            int parameters = ImageBytes.CompressedUnsigned(image, start + 1, end, cursor);
            if (parameters < 0 || cursor[0] >= end) return -1;
            if (parameters > depth) return CallConsumesAddress;
            int result = image[cursor[0]];
            if (result == 0x1f || result == 0x20) return -1; // Return custom modifiers.
            if (construction && result != 1) return -1;
            return depth - parameters + (construction || result != 1 ? 1 : 0);
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
