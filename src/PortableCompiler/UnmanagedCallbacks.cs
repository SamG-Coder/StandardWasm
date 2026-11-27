using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0098: a static unmanaged function pointer as a host-callable token.
    //
    // Managed code holds an unmanaged callback as a table-2 slot (decision 0078), and the
    // callee keeps its lowered managed signature: a Foundation wrapper such as HWND is a
    // GC struct that host JavaScript cannot construct. Each distinct callback signature
    // therefore gets one synthetic entry function, `(i32 target, word...) -> word`, that
    // boxes the ABI words into the callee's lowered types and calls the target through
    // the signature's carrier. The entry also sits in table 2, so an import receives
    // the token as (target slot, entry funcref) and the host calls `entry(target, ...words)`.
    // Nothing here is specific to one Win32 API; EnumThreadWindows is the first consumer.
    internal sealed class UnmanagedCallbackPlan
    {
        internal int[] Carriers = new int[4]; // FunctionPointerPlan carrier chain per entry
        internal int[] Entries = new int[4];  // synthetic entry function index
        internal int[] Slots = new int[4];    // the entry's own table-2 slot
        internal int Count;

        internal int Find(int carrier)
        {
            for (int i = 0; i < Count; i++)
                if (Carriers[i] == carrier) return i;
            return -1;
        }

        internal int Add(int carrier, int entry, int slot)
        {
            if (Count == Carriers.Length)
            {
                Carriers = Grow(Carriers);
                Entries = Grow(Entries);
                Slots = Grow(Slots);
            }
            Carriers[Count] = carrier;
            Entries[Count] = entry;
            Slots[Count] = slot;
            return Count++;
        }

        private static int[] Grow(int[] values)
        {
            var grown = new int[values.Length * 2];
            for (int i = 0; i < values.Length; i++) grown[i] = values[i];
            return grown;
        }
    }

    internal static class UnmanagedCallbackSignatures
    {
        internal const int MaximumParameters = 8;
        private const byte FunctionPointer = 0x1b, Stdcall = 0x02, PlatformUnmanaged = 0x09;

        // ELEMENT_TYPE_FNPTR with the Windows x86 callback convention: explicit stdcall,
        // or the platform-default unmanaged convention, which is stdcall there. Other
        // conventions (cdecl, thiscall, managed) stay unmatched so callers diagnose them.
        internal static bool ReadHeader(byte[] image, int[] cursor, int end, int parameters) =>
            cursor[0] + 2 < end && image[cursor[0]] == FunctionPointer &&
            (image[cursor[0] + 1] == Stdcall || image[cursor[0] + 1] == PlatformUnmanaged) &&
            image[cursor[0] + 2] == parameters && Take(cursor, 3);

        private static bool Take(int[] cursor, int count)
        {
            cursor[0] += count;
            return true;
        }

        // Reads the embedded result and parameter storage types after ReadHeader.
        // types[0] is the result. Every type must cross the host as one ABI word.
        internal static bool ReadTypes(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] cursor, int end, int[] objects, int[] types, int parameters)
        {
            if (parameters > MaximumParameters || types.Length <= parameters) return false;
            for (int i = 0; i <= parameters; i++)
            {
                int type = ManagedReferenceSignatures.ReadStorage(bundle, image, layout, heaps, owner, cursor, end, 0, objects);
                if (type < 0 || AbiWord(objects, type) == 0 || (i != 0 && type == Void)) return false;
                types[i] = type;
            }
            return true;
        }

        // The Wasm word a storage type crosses as: I32 or Native (both i32 on wasm32),
        // Void for a result, or the single instance field of a sequential blittable
        // wrapper such as HWND, LPARAM or BOOL. Anything else returns 0.
        internal static int AbiWord(int[] objects, int type)
        {
            if (type == I32 || type == Native || type == Void) return type;
            if (!ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.IsExplicitValue(objects, type) ||
                ObjectLayouts.IsAutoValue(objects, type) || ObjectLayouts.IsByRefLikeValue(objects, type) ||
                ObjectLayouts.IsInlineArray(objects, type))
                return 0;
            int word = 0;
            for (int index = 0; index < ObjectLayouts.FieldCount(objects, type); index++)
            {
                int storage = ObjectLayouts.FieldTypeAt(objects, type, index);
                if (storage == 0) continue; // static field slot
                if (word != 0 || storage != I32 && storage != Native) return 0;
                word = storage;
            }
            return word;
        }
    }

    internal static class UnmanagedCallbackWasm
    {
        internal const int EntryCapacity = 96 + 16 * UnmanagedCallbackSignatures.MaximumParameters;

        // The entry's own signature: the ABI result, the i32 target slot, then one word per parameter.
        internal static void EntrySignature(int[] objects, int[] callback, int parameters, int[] entry)
        {
            entry[0] = UnmanagedCallbackSignatures.AbiWord(objects, callback[0]);
            entry[1] = I32;
            for (int i = 1; i <= parameters; i++) entry[i + 1] = UnmanagedCallbackSignatures.AbiWord(objects, callback[i]);
        }

        // Body of the entry. Locals: 0 = target slot, 1..n = ABI words, n + 1 = lowered result.
        internal static int WriteEntry(byte[] code, int used, int[] objects, int[] callback, int parameters,
            FunctionPointerPlan pointers, int carrier)
        {
            int result = parameters + 1;
            bool returns = callback[0] != Void;
            if (returns)
            {
                code[used++] = 1;
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, callback[0]);
            }
            else code[used++] = 0;

            // block $escaped; try_table (catch_all $escaped)
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x1f; code[used++] = 0x40; code[used++] = 1;
            code[used++] = 0x02; code[used++] = 0; // catch_all -> $escaped
            for (int i = 1; i <= parameters; i++)
            {
                used = Local(code, used, 0x20, i);
                if (callback[i] != I32 && callback[i] != Native) used = Gc(code, used, 0, callback[i]); // struct.new wrapper
            }
            used = Local(code, used, 0x20, 0);
            used = FunctionPointerWasm.CallIndirect(code, used, 0, pointers, carrier);
            if (returns)
            {
                used = Local(code, used, 0x21, result);
                used = ReadWord(code, used, callback[0], result);
            }
            code[used++] = 0x0f; // return the ABI word
            code[used++] = 0x0b; // end try_table
            code[used++] = 0x0b; // end block
            // The CLR fail-fasts when an exception escapes an UnmanagedCallersOnly method;
            // it never unwinds into the native caller. A trap cannot be intercepted by
            // managed catch/finally either, so the launch ends instead of resuming the caller.
            code[used++] = 0x00; // unreachable
            code[used++] = 0x0b;
            return used;
        }

        // Pushes the host-callable entry as a funcref: table.get 2 (slot).
        internal static int LoadEntry(byte[] code, int used, int slot)
        {
            used = Const(code, used, slot);
            code[used++] = 0x25;
            code[used++] = FunctionPointerPlan.Table;
            return used;
        }

        // A lowered word-sized value as its ABI word. A null wrapper reference is the
        // default value, so materialize it before reading the field.
        internal static int ReadWord(byte[] code, int used, int type, int local)
        {
            if (type == I32 || type == Native) return Local(code, used, 0x20, local);
            used = ValueFieldsWasm.Materialize(code, used, type, local);
            used = Gc(code, used, 2, type); // struct.get type 0
            code[used++] = 0;
            return used;
        }
    }
}
