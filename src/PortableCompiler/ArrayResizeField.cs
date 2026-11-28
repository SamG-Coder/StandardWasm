using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: `Array.Resize<T>(ref r.F, newSize)` for a primitive T and a class field F,
    // in the shape C# emits:
    //   ldarg/ldloc r; ldflda T[] F; <newSize: pure straight-line arithmetic>; call Resize<T>(!!0[]&, int32)
    // The compiler has no location for an array-typed field, so the byref never materializes:
    //   ldflda F becomes ldfld F (the CLR reads the array inside Resize; the size expression only
    //   loads locals, arguments, fields and constants, so nothing can write F in between), and
    //   the call becomes ResizeArrayField: [old array, newSize] -> [], storing the result into r.F
    //   with r reloaded from its local, which the size expression cannot assign.
    // Resize itself follows the CLR: newSize < 0 -> ArgumentOutOfRangeException; a null array
    // becomes new T[newSize]; a different length copies min(length, newSize) elements into a new
    // array; the same length keeps the array (and its identity).
    internal static class ArrayResizeField
    {
        internal const int Capacity = 120;

        // The call record: operand = the ldflda record index; aux = receiver local << 8 | element.
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            int[] counts = null;
            for (int call = 0; call < records.Length; call += Width)
            {
                if (records[call] != MethodSpecCall || records[call + 3] != 0) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                }
                int element = Element(image, layout, heaps, counts, records[call + 1]);
                if (element == 0) continue;
                // Walk back over the size expression to the ldflda.
                int field = -1, depth = 0;
                for (int at = Previous(records, clauses, call); at >= 0; at = Previous(records, clauses, at))
                {
                    if (depth == 1 && records[at] == FieldAddress) { field = at; break; }
                    int effect = Effect(records[at]);
                    if (effect == Impure) break;
                    depth += effect;
                    if (depth > 1) break;
                }
                if (field < 0 || !Balanced(records, field, call)) continue;
                int receiver = Previous(records, clauses, field);
                if (receiver < 0 || records[receiver] != Load || records[receiver + 1] < 0) continue;
                records[field] = GetField;
                records[call] = ResizeArrayField;
                records[call + 1] = field / Width;
                records[call + 3] = records[receiver + 1] << 8 | element;
            }
        }

        private const int Impure = int.MinValue;

        // Net stack effect of a pure size-expression record, or Impure.
        private static int Effect(int kind) =>
            kind == Load || kind == Constant || kind == WideConstant ? 1 :
            kind == GetField || kind == ArrayLength || kind == ConvertInteger || kind == Unary || kind == Nop ? 0 :
            kind == Binary ? -1 : Impure;

        // The records strictly between `field` and `call` push exactly one value and never consume
        // a value from below their own start (the ldflda's byref).
        private static bool Balanced(int[] records, int field, int call)
        {
            int depth = 0;
            for (int at = records[field + 2] * Width; at != call; at = records[at + 2] * Width)
            {
                int kind = records[at];
                int pops = kind == GetField || kind == ArrayLength || kind == ConvertInteger || kind == Unary ? 1 :
                    kind == Binary ? 2 : 0;
                if (depth < pops) return false;
                depth += Effect(kind);
            }
            return depth == 1;
        }

        // The unique straight-line predecessor of `slot` that is not a branch target, or -1.
        private static int Previous(int[] records, int[] clauses, int slot)
        {
            if (ValueOperands.Entry(records, slot / Width, clauses)) return -1;
            int found = -1;
            for (int at = 0; at < records.Length; at += Width)
                if (records[at] != 0 && records[at] != Branch && records[at] != Return && records[at + 2] * Width == slot)
                {
                    if (found >= 0) return -1;
                    found = at;
                }
            return found;
        }

        // The primitive element of `void Array.Resize<T>(!!0[]&, int32)`, or 0.
        private static int Element(byte[] image, int[] layout, int[] heaps, int[] counts, int row)
        {
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0 ||
                spec[MethodSpecifications.Arity] != 1 ||
                spec[MethodSpecifications.Arguments] + 1 != spec[MethodSpecifications.End]) return 0;
            int element = image[spec[MethodSpecifications.Arguments]];
            if (PinnedArrays.Vector(element) == 0) return 0;
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) == 0) return 0;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "Resize") ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                return 0;
            int blob = member[5];
            if (blob < 1 || !Fits((uint)blob, 9, (uint)heaps[1])) return 0;
            uint s = (uint)(heaps[0] + blob);
            // void Resize<T>(!!0[]&, int32): 08 10 01 02 01 10 1d 1e 00 08
            return image[s] == 9 && image[s + 1] == 0x10 && image[s + 2] == 1 && image[s + 3] == 2 &&
                image[s + 4] == 1 && image[s + 5] == 0x10 && image[s + 6] == 0x1d && image[s + 7] == 0x1e &&
                image[s + 8] == 0 && image[s + 9] == 0x08 ? element : 0;
        }

        internal static int Vector(int aux) => PinnedArrays.Vector(aux & 0xff);

        // After classification: the record index of the first ResizeArrayField whose field read did
        // not stay a plain class field of the element's array type, or whose receiver local is
        // not an ordinary Wasm local; -1 when every one qualifies.
        internal static int Validate(int[] records, int[] objects, int[] frameCells, int[] inlineLocations)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != ResizeArrayField) continue;
                int field = records[slot + 1] * Width, aux = records[slot + 3], receiver = Receiver(aux);
                bool plain = field < records.Length && records[field] == GetField;
                int owner = plain ? FieldAccess.Owner(records[field + 3]) : 0;
                int storage = plain ? FieldAccess.Storage(records[field + 3]) : 0;
                if (!plain || storage != Vector(aux) || ObjectLayouts.IsValueType(objects, owner) ||
                    ObjectLayouts.HasExplicitSlots(objects, owner) || ManagedValueLocationWasm.UsesCell(objects, owner, storage) ||
                    receiver >= frameCells.Length || frameCells[receiver] != 0 || inlineLocations[receiver] != 0)
                    return slot / Width;
            }
            return -1;
        }

        internal static int Receiver(int aux) => aux >> 8;

        // [old, size] -> []: r.F = Resize(old, size). `fresh` is a scratch local of the vector type;
        // `old` receives the result, as the old array is dead after the copy.
        internal static int Emit(byte[] code, int used, int vector, int old, int size, int receiver,
            int owner, int fieldIndex, int fresh)
        {
            // size < 0 -> ArgumentOutOfRangeException
            used = Local(code, used, 0x20, size);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            // if (old == null || old.length != size) { fresh = new T[size]; copy; old = fresh }
            used = Local(code, used, 0x20, old);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x7f; // if (result i32)
            used = Const(code, used, 1);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, old);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x20, size);
            code[used++] = 0x47; // i32.ne
            code[used++] = 0x0b;
            code[used++] = 0x04; code[used++] = 0x40; // if
            used = Local(code, used, 0x20, size);
            used = Gc(code, used, 0x07, vector); // array.new_default
            used = Local(code, used, 0x21, fresh);
            used = Local(code, used, 0x20, old);
            code[used++] = 0xd1;
            code[used++] = 0x45; // non-null
            code[used++] = 0x04; code[used++] = 0x40;
            // array.copy fresh[0..] <- old[0..min(length, size)]
            used = Local(code, used, 0x20, fresh);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, old);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, old);
            code[used++] = 0xfb; code[used++] = 0x0f;
            used = Local(code, used, 0x20, size);
            used = Local(code, used, 0x20, old);
            code[used++] = 0xfb; code[used++] = 0x0f;
            used = Local(code, used, 0x20, size);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x1b; // select: min(length, size)
            used = Gc(code, used, 0x11, vector); // array.copy: destination type, then source type
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(vector));
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, fresh);
            used = Local(code, used, 0x21, old);
            code[used++] = 0x0b;
            // r.F = old
            used = GuardNull(code, used, receiver);
            used = Local(code, used, 0x20, receiver);
            used = Local(code, used, 0x20, old);
            used = Gc(code, used, 5, owner); // struct.set
            return WasmBinary.Unsigned(code, used, fieldIndex);
        }
    }
}
