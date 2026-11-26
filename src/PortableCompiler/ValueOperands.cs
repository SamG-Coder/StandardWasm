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
                return Clear(main, bundle, ranges, image, layout, heaps, owner, records, slot, next, type, objects);
            if (kind == GetField)
            {
                records[slot] = Load;
                return 0;
            }
            return LowerAddressUse(records, slot, local, clauses);
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
        internal static int LowerReceiver(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] records, int slot, int[] frame, int[] objects, int[] clauses, bool readOnly = false)
        {
            int next = records[slot + 2] * Width;
            if (next >= records.Length || Entry(records, next / Width, clauses)) return -2;
            int kind = records[next];
            // [&this] -> ldfld -> [field]. The load already pushes the current value.
            if (kind == GetField) return 0;
            if (kind == InitializeValue)
                return readOnly ? -2 : Clear(main, bundle, ranges, image, layout, heaps, owner, records, slot, next, frame[0], objects);
            return LowerAddressUse(records, slot, 0, clauses, readOnly);
        }

        // ldloca/ldarg.0 followed by initobj of the same type clears the location.
        private static int Clear(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] records, int slot, int next, int type, int[] objects)
        {
            uint token = unchecked((uint)records[next + 1]);
            uint table = token >> 24;
            int row = (int)(token & 0xffffff);
            int initialized;
            if (table == 0x1b)
            {
                int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
                if (blob < 0) return blob;
                initialized = ManagedReferenceSignatures.ReadInstantiation(main, bundle, ranges, image, layout, heaps,
                    owner, blob, objects);
            }
            else
            {
                if (table != 1 && table != 2) return -2;
                int coded = row << 2 | (table == 1 ? 1 : 0);
                initialized = BundleLayouts.RegisterType(main, bundle, ranges, image, layout, heaps, owner, coded, objects, true);
            }
            if (initialized < 0 || initialized != type) return -2;
            // The operand stays the frame index: the local, or argument 0.
            records[slot] = ClearValue;
            records[slot + 2] = records[next + 2];
            records[next] = 0;
            return 0;
        }

        // A writable address can survive only a straight-line calculation
        // that ends in stfld or in a call on that address. Track the
        // temporary stack separately so the sequence cannot consume values
        // below the original address or pass it anywhere else.
        private static int LowerAddressUse(int[] records, int slot, int local, int[] clauses, bool readOnly = false)
        {
            int next = records[slot + 2] * Width;
            int depth = 0;
            while (next < records.Length)
            {
                if (Entry(records, next / Width, clauses)) return -2;
                int kind = records[next];
                if (kind == SetField && depth == 1)
                {
                    if (readOnly) return -2;
                    // [&value, field] -> stfld -> []. UpdateValue replaces the local's aggregate.
                    records[slot] = Nop;
                    records[next + Width + 1] = local + 1;
                    return 0;
                }
                if (kind == Call && records[next + 3] == 0 || kind == MemberCall && records[next + 3] == 0x28)
                {
                    // [&value, arguments] -> call -> []. The link step accepts only
                    // a value-type instance constructor (ConstructValue) or a
                    // read-only instance method (decision 0049) with exactly
                    // these arguments. The receiver is pushed as the
                    // current value, and the call's spare operand records keep
                    // the location and the argument count.
                    if (records[slot] == LocalAddress) records[slot] = Load;
                    records[next + Width + 1] = local + 1;
                    records[next + 2 * Width + 1] = depth + 1;
                    return 0;
                }
                if (kind == Load || kind == Constant || kind == WideConstant || kind == SingleConstant ||
                    kind == DoubleConstant || kind == NullLiteral || kind == LoadString) depth++;
                else if (kind == Binary || kind == CheckedInteger)
                {
                    if (depth < 2) return -2;
                    depth--;
                }
                // A field read of an operand above the address (for example
                // this.X = this.Y) observes the location's current contents.
                else if (kind != Nop && (depth == 0 || kind != Unary && kind != Narrow && kind != GetField &&
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
