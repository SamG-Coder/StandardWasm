using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Stage 204 (issue #256, decision 0091): delegate types beyond the fixed Func<int>, EventHandler and
    // SpanAction<char, IntPtr> shapes. A closed System.Func`1..`5 or System.Action`1..`4 instantiation
    // whose arguments have Wasm storage gets a DelegateShapes key. Its value is the shared heap-3
    // closure (eqref target, funcref method), whose method is a thunk of type
    // (eqref target, p1..pn) -> r (appended after the fixed types; WasmBinary).
    //
    //   newobj D::.ctor(object, native int)   NewDelegate (instance or open static target)
    //   callvirt D::Invoke(p1..pn)            Call to the key's invoke helper, which does
    //                                         struct.get target; args; struct.get method;
    //                                         ref.cast (ref thunk); call_ref
    //
    // Func<int> keeps its existing Delegate storage (decision 0088). Casts that would need to tell two
    // delegate types apart at run time stay diagnosed: the shared closure has no type identity.
    internal static class GeneralDelegates
    {
        internal const int Construct = 1, Invoke = 2;

        // The key for a GENERICINST of `coded` with these arguments (generic argument storages), 0 when it
        // is not a supported platform delegate, or a negative status.
        internal static int Storage(byte[] image, int[] layout, int[] heaps, int coded, int[] arguments, int count, int[] objects)
        {
            int kind = 0;
            if (count == 1 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Func`1") ||
                count == 2 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Func`2") ||
                count == 3 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Func`3") ||
                count == 4 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Func`4") ||
                count == 5 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Func`5"))
                kind = DelegateShapes.PlatformFunc;
            else if (count == 1 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Action`1") ||
                count == 2 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Action`2") ||
                count == 3 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Action`3") ||
                count == 4 && SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "Action`4"))
                kind = DelegateShapes.PlatformAction;
            if (kind == 0) return 0;
            var parameters = new int[count];
            for (int i = 0; i < count; i++)
            {
                parameters[i] = CliStorage(objects, arguments[i]);
                if (parameters[i] == 0) return 0; // no Wasm storage: keep the generic-instance reading
            }
            int parameterCount = kind == DelegateShapes.PlatformFunc ? count - 1 : count;
            int result = kind == DelegateShapes.PlatformFunc ? parameters[count - 1] : Void;
            // Func<int> keeps the scalar Delegate closure of decision 0088.
            if (kind == DelegateShapes.PlatformFunc && count == 1 && result == I32) return 0;
            return DelegateShapes.Register(objects, kind, -1, count, result, parameters, parameterCount);
        }

        // A generic argument's signature storage when a delegate parameter or result can carry it.
        private static int CliStorage(int[] objects, int argument)
        {
            int storage = ObjectLayouts.IsEnumKey(argument) || argument >= 0x200 && argument < 0x260
                ? ObjectLayouts.SignatureStorage(objects, argument) : argument;
            if (storage == I32 || IsNarrow(storage) || storage == I64 || storage == F32 || storage == F64 || storage == Native ||
                storage == String || storage == ObjectLayouts.ObjectReference || DelegateShapes.IsKey(storage) ||
                storage == Delegate)
                return storage;
            if (storage >= ObjectLayouts.FirstClass && storage < ObjectLayouts.FirstClass + ObjectLayouts.MaxClasses &&
                !ObjectLayouts.IsValueType(objects, storage) && !ObjectLayouts.IsInterface(objects, storage))
                return storage;
            return 0;
        }

        // Construct or Invoke for a MemberRef on a supported delegate instantiation; key[0] receives its key.
        internal static int Member(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int context, int[] methodArguments, int[] objects, int[] key)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0)
                return 0;
            uint name = (uint)reference[4];
            bool construct = MatchesAscii(image, heaps, name, ".ctor");
            if (!construct && !MatchesAscii(image, heaps, name, "Invoke")) return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent <= 0) return 0;
            int type = ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                owner, parent, objects, context, methodArguments);
            if (type < 0 || !DelegateShapes.IsKey(type)) return 0;
            int blob = reference[5];
            if (blob <= 0 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 3 || size > limit - start || image[start] != 0x20) return 0;
            if (construct)
            {
                // instance void .ctor(object, native int)
                if (size != 5 || image[start + 1] != 2 || image[start + 2] != 1 || image[start + 3] != 0x1c ||
                    image[start + 4] != 0x18) return 0;
                key[0] = type;
                return Construct;
            }
            // instance !n Invoke(!0 .. !n-1) or instance void Invoke(!0 .. !n-1): the parameters are the
            // generic parameters in order, so the shape is the instantiation's own.
            int count = DelegateShapes.InvokeParameterCount(objects, type);
            bool hasResult = DelegateShapes.InvokeResult(objects, type) != Void;
            if (image[start + 1] != count) return 0;
            int at = start + 2;
            if (hasResult)
            {
                if (image[at] != 0x13 || image[at + 1] != count) return 0;
                at += 2;
            }
            else if (image[at++] != 1) return 0;
            for (int i = 0; i < count; i++)
            {
                if (at + 1 >= start + size || image[at] != 0x13 || image[at + 1] != i) return 0;
                at += 2;
            }
            if (at != start + size) return 0;
            key[0] = type;
            return Invoke;
        }

        // Whether target (signature at `shape`, `arity` operands) can be the method of a delegate with
        // this key: an instance target takes the receiver first, an open static target does not, and
        // every parameter and the result match the Invoke signature exactly.
        internal static bool MatchesTarget(int[] objects, int key, int[] signatures, int shape, int arity, bool instance)
        {
            int count = DelegateShapes.InvokeParameterCount(objects, key);
            if (count < 0 || arity != count + (instance ? 1 : 0)) return false;
            if (signatures[shape] != DelegateShapes.InvokeResult(objects, key)) return false;
            int first = shape + 1 + (instance ? 1 : 0);
            for (int i = 0; i < count; i++)
                if (signatures[first + i] != DelegateShapes.InvokeParameter(objects, key, i)) return false;
            return true;
        }

        // The key's invoke helper, registering it as a synthetic function (rows 0) on first use. The
        // body is written after discovery (WriteInvokeHelper). Returns the updated function count.
        internal static int RegisterInvokeHelper(int[] objects, int key, int owner, int[] owners, int[] rows, int[] contexts,
            int[] arities, int[] offsets, int[] signatures, int functions)
        {
            if (DelegateShapes.InvokeHelper(objects, key) >= 0) return functions;
            if (functions >= owners.Length) return -3;
            int count = DelegateShapes.InvokeParameterCount(objects, key);
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (count + 1 >= signatures.Length - offset) return -3;
            signatures[offset] = DelegateShapes.InvokeResult(objects, key);
            signatures[offset + 1] = key; // the closure receiver
            for (int i = 0; i < count; i++) signatures[offset + 2 + i] = DelegateShapes.InvokeParameter(objects, key, i);
            owners[functions] = owner;
            rows[functions] = 0; // synthetic, never compiled from IL
            contexts[functions] = 0;
            arities[functions] = count + 1;
            offsets[functions] = offset;
            DelegateShapes.SetInvokeHelper(objects, key, functions);
            return functions + 1;
        }

        // The thunk table keeps, in its second half, each thunk's delegate key: positive for an instance
        // target, negative for an open static target, 0 for the fixed shapes (Func<int>, EventHandler,
        // SpanAction<char, IntPtr>), whose thunk type and static-ness come from the target's arity.
        internal static int ThunkKey(int[] thunks, int thunk) => thunks[thunks.Length / 2 + thunk];

        internal static void SetThunkKey(int[] thunks, int thunk, int key, bool open) =>
            thunks[thunks.Length / 2 + thunk] = open ? -key : key;

        // The appended function types, one per delegate entry in ordinal order:
        // (eqref target, p1..pn) -> r.
        internal static int ThunkTypeBytes(int[] objects)
        {
            var scratch = new byte[16];
            int size = 0;
            for (int ordinal = 0; ordinal < DelegateShapes.EntryCount(objects); ordinal++)
            {
                int key = DelegateShapes.KeyAt(ordinal);
                int count = DelegateShapes.InvokeParameterCount(objects, key), result = DelegateShapes.InvokeResult(objects, key);
                size += 1 + WasmBinary.UnsignedSize(count + 1) + 1 + 1;
                for (int i = 0; i < count; i++)
                    size += WasmBinary.ValueType(scratch, 0, DelegateShapes.InvokeParameter(objects, key, i));
                size += result == Void ? 0 : WasmBinary.ValueType(scratch, 0, result);
            }
            return size;
        }

        internal static int WriteThunkTypes(byte[] output, int cursor, int[] objects)
        {
            for (int ordinal = 0; ordinal < DelegateShapes.EntryCount(objects); ordinal++)
            {
                int key = DelegateShapes.KeyAt(ordinal);
                int count = DelegateShapes.InvokeParameterCount(objects, key), result = DelegateShapes.InvokeResult(objects, key);
                output[cursor++] = 0x60;
                cursor = WasmBinary.Unsigned(output, cursor, count + 1);
                output[cursor++] = 0x6d; // eqref target
                for (int i = 0; i < count; i++)
                    cursor = WasmBinary.ValueType(output, cursor, DelegateShapes.InvokeParameter(objects, key, i));
                if (result == Void) output[cursor++] = 0;
                else
                {
                    output[cursor++] = 1;
                    cursor = WasmBinary.ValueType(output, cursor, result);
                }
            }
            return cursor;
        }

        // (closure, p1..pn) -> r. The caller's callvirt null check already ran, so the closure is
        // non-null: read the target and method, cast the method to the key's thunk type and call it.
        // The thunk type index is a fixup chain per key (typeFixups[key - KeyBase]), resolved by
        // WasmBinary once the appended types have indices.
        internal static int WriteInvokeHelper(byte[] code, int used, int[] objects, int key, int[] typeFixups)
        {
            int count = DelegateShapes.InvokeParameterCount(objects, key);
            if (code.Length - used < 32 + 2 * count) return -3;
            code[used++] = 0; // no additional locals
            used = BranchWasm.Local(code, used, 0x20, 0);
            code[used++] = 0xfb; code[used++] = 2; code[used++] = 3; code[used++] = 0; // struct.get closure.target
            for (int i = 0; i < count; i++) used = BranchWasm.Local(code, used, 0x20, 1 + i);
            used = BranchWasm.Local(code, used, 0x20, 0);
            code[used++] = 0xfb; code[used++] = 2; code[used++] = 3; code[used++] = 1; // struct.get closure.method
            code[used++] = 0xfb; code[used++] = 0x16; // ref.cast (ref thunk)
            used = WasmIndexFixups.WriteAt(code, used, 0, typeFixups, key - DelegateShapes.KeyBase);
            code[used++] = 0x14; // call_ref thunk
            used = WasmIndexFixups.WriteAt(code, used, 0, typeFixups, key - DelegateShapes.KeyBase);
            code[used++] = 0x0b;
            return used;
        }
    }
}
