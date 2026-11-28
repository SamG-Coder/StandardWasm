using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: a class instance field of an [InlineArray(N)] value type A whose element is a
    // plain sequential struct E of k fields that all share one 32- or 64-bit integer storage.
    //
    // CLR: the N elements are laid out back to back; code reaches element i as
    //   ref E e = ref Unsafe.Add(ref Unsafe.As<A, E>(ref this.F), i);
    // and then reads and writes e.X. Wasm GC struct fields cannot be indexed at run time, so the
    // field is stored as one flat Ints/Longs vector of N * k elements, allocated on first use
    // (all zero, as the CLR's default value). Element i field X is vector[i * k + ordinal(X)].
    //
    // The reference e becomes a FlatInlineLocation(E): an i32 holding i * k. Its vector is always
    // re-read from this.F: `this` is argument 0, which the method never stores, and F can only
    // change through this lowering because any other IL use of F (a value copy, a store, another
    // address) sees a vector where the CLI expects A and fails verification, as a located
    // diagnostic. Unsafe.Add does not check bounds on the CLR (undefined behavior beyond N); here
    // an index outside [0, N) traps at the array access.
    internal static class FlatInlineArrays
    {
        // Verifier tags FlatInlineLocation(E), between the reference locations and value locations.
        private const int LocationBase = 40000;
        private const int WideFlag = 1 << 30;
        internal const int Capacity = 96;

        internal static int Location(int element) => LocationBase + element;

        internal static bool IsLocation(int type) =>
            type >= LocationBase + ObjectLayouts.FirstClass &&
            type < LocationBase + ObjectLayouts.FirstClass + ObjectLayouts.MaxClasses;

        internal static int Element(int location) => location - LocationBase;

        // The element struct E of a FlatIndex record's aux.
        internal static int IndexElement(int aux) => aux >> 16;

        // The flat vector storage for a field of inline-array type `inline`, or 0.
        internal static int Storage(int[] objects, int inline)
        {
            if (!ObjectLayouts.IsValueType(objects, inline) || !ObjectLayouts.IsInlineArray(objects, inline)) return 0;
            int element = ObjectLayouts.LayoutFieldType(objects, inline, 0);
            int scalar = Scalar(objects, element);
            return scalar == I32 ? Ints : scalar == I64 ? Longs : 0;
        }

        // The one scalar storage of all of E's fields (I32 or I64), or 0.
        private static int Scalar(int[] objects, int element)
        {
            if (!ObjectLayouts.IsValueType(objects, element) || ObjectLayouts.IsExplicitValue(objects, element) ||
                ObjectLayouts.IsInlineArray(objects, element) || ObjectLayouts.IsByRefLikeValue(objects, element)) return 0;
            int count = ObjectLayouts.FieldCount(objects, element);
            if (count < 1) return 0;
            int scalar = ObjectLayouts.LayoutFieldType(objects, element, 0);
            if (scalar != I32 && scalar != I64) return 0;
            for (int i = 1; i < count; i++)
                if (ObjectLayouts.LayoutFieldType(objects, element, i) != scalar) return 0;
            return scalar;
        }

        // Rewrites `ldarg.0; ldflda F; call Unsafe.As<A, E>; <i>; call Unsafe.Add<E>` into
        // `<i>; FlatIndex` and retypes the local that stores the result. Returns 0, or -2 with
        // `location` set when a method uses the shape on a field whose layout is not flat.
        internal static int Lower(byte[] image, int[] layout, int[] heaps, int owner, int[] objects,
            int[] records, int[] clauses, int[] frame, int parameters, int[] location)
        {
            int[] counts = null;
            int field = 0;
            for (int add = 0; add < records.Length; add += Width)
            {
                if (records[add] != MethodSpecCall || records[add + 3] != 0) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
                }
                int element = UnsafeMember(image, layout, heaps, counts, owner, objects, records[add + 1], "Add", 1, 0);
                if (element == 0) continue;
                // Walk back over the index expression to the Unsafe.As call.
                int cast = -1, depth = 0;
                for (int at = Previous(records, clauses, add); at >= 0; at = Previous(records, clauses, at))
                {
                    if (depth == 1 && records[at] == MethodSpecCall) { cast = at; break; }
                    int effect = Effect(records[at]);
                    if (effect == int.MinValue) break;
                    depth += effect;
                    if (depth > 1) break;
                }
                if (cast < 0 || !ArrayResizeField.Balanced(records, cast, add)) continue;
                int inline = UnsafeMember(image, layout, heaps, counts, owner, objects, records[cast + 1], "As", 2, 0);
                if (inline == 0 ||
                    UnsafeMember(image, layout, heaps, counts, owner, objects, records[cast + 1], "As", 2, 1) != element) continue;
                int address = Previous(records, clauses, cast), receiver = address < 0 ? -1 : Previous(records, clauses, address);
                if (receiver < 0 || records[address] != FieldAddress || records[address + 3] != 0 ||
                    records[receiver] != Load || records[receiver + 1] != 0 || parameters == 0) continue;
                int store = records[add + 2] * Width;
                if (store >= records.Length || records[store] != Store || records[store + 1] < parameters ||
                    ValueOperands.Entry(records, store / Width, clauses)) continue;
                int type = frame[0];
                int storage = ObjectLayouts.FieldType(objects, type, records[address + 1]);
                int index = ObjectLayouts.FieldOrdinal(objects, type, records[address + 1]);
                if (storage == 0 || storage != Storage(objects, inline) ||
                    ObjectLayouts.LayoutFieldType(objects, inline, 0) != element || index < 0 ||
                    field != 0 && field != index + 1 || WritesThis(records))
                {
                    if (location.Length != 0) location[2] = address / Width;
                    return -2;
                }
                field = index + 1;
                int k = ObjectLayouts.FieldCount(objects, element);
                int length = ObjectLayouts.FieldCount(objects, inline) * k;
                records[receiver] = Nop;
                records[address] = Nop;
                records[cast] = Nop;
                records[add] = FlatIndex;
                records[add + 1] = k | length << 8 | (storage == Longs ? WideFlag : 0);
                records[add + 3] = index | element << 16;
                frame[records[store + 1]] = Location(element);
            }
            return 0;
        }

        // Pure straight-line index expressions only, as in ArrayResizeField.
        private static int Effect(int kind) =>
            kind == Load || kind == Constant || kind == WideConstant ? 1 :
            kind == GetField || kind == ArrayLength || kind == ConvertInteger || kind == Unary || kind == Nop ? 0 :
            kind == Binary ? -1 : int.MinValue;

        private static bool WritesThis(int[] records)
        {
            for (int at = 0; at < records.Length; at += Width)
                if ((records[at] == Store || records[at] == FrameAddress) && records[at + 1] == 0) return true;
            return false;
        }

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

        // For `!!1& Unsafe.As<A, E>(!!0&)` (arity 2) or `!!0& Unsafe.Add<E>(!!0&, int32)` (arity 1):
        // the registered type of generic argument `which`, a TypeDef value type of this assembly; else 0.
        private static int UnsafeMember(byte[] image, int[] layout, int[] heaps, int[] counts, int owner, int[] objects,
            int row, string name, int arity, int which)
        {
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0 ||
                spec[MethodSpecifications.Arity] != arity) return 0;
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) == 0) return 0;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], name) ||
                !MatchesAscii(image, heaps, (uint)member[2], "Unsafe") ||
                !MatchesAscii(image, heaps, (uint)member[3], "System.Runtime.CompilerServices") ||
                !PlatformAssemblyIdentity.IsUnsafeAssembly(image, heaps, layout, counts, member[AssemblyReferences.AssemblyRow]))
                return 0;
            int blob = member[5];
            if (blob < 1 || !Fits((uint)blob, 11, (uint)heaps[1])) return 0;
            uint s = (uint)(heaps[0] + blob);
            // As:  09 10 02 01 10 1e 01 10 1e 00        Add: 0a 10 01 02 10 1e 00 10 1e 00 08
            bool shape = arity == 2
                ? image[s] == 9 && image[s + 1] == 0x10 && image[s + 2] == 2 && image[s + 3] == 1 &&
                  image[s + 4] == 0x10 && image[s + 5] == 0x1e && image[s + 6] == 1 &&
                  image[s + 7] == 0x10 && image[s + 8] == 0x1e && image[s + 9] == 0
                : image[s] == 10 && image[s + 1] == 0x10 && image[s + 2] == 1 && image[s + 3] == 2 &&
                  image[s + 4] == 0x10 && image[s + 5] == 0x1e && image[s + 6] == 0 &&
                  image[s + 7] == 0x10 && image[s + 8] == 0x1e && image[s + 9] == 0 && image[s + 10] == 0x08;
            if (!shape) return 0;
            var next = new int[1];
            int at = spec[MethodSpecifications.Arguments], end = spec[MethodSpecifications.End];
            for (int i = 0; i <= which; i++)
            {
                if (at >= end || image[at] != 0x11) return 0;
                int coded = CompressedUnsigned(image, at + 1, end, next);
                if (coded < 0) return 0;
                at = next[0];
                if (i < which) continue;
                if ((coded & 3) != 0) return 0; // a TypeDef of this assembly
                int type = ObjectLayouts.Find(objects, owner, coded >> 2, new int[0]);
                return type > 0 && ObjectLayouts.IsValueType(objects, type) ? type : 0;
            }
            return 0;
        }

        // [i] -> [i * k]: also allocates this.F on first use.
        internal static int EmitIndex(byte[] code, int used, int[] objects, int owner, int operand, int field,
            int index, int target)
        {
            int k = operand & 0xff, length = (operand & ~WideFlag) >> 8;
            int vector = (operand & WideFlag) != 0 ? Longs : Ints;
            used = Local(code, used, 0x20, 0);
            used = Gc(code, used, 2, owner); // struct.get
            used = WasmBinary.Unsigned(code, used, field);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, 0);
            used = Const(code, used, length);
            used = Gc(code, used, 0x07, vector); // array.new_default: the CLR's zeroed elements
            used = Gc(code, used, 5, owner); // struct.set
            used = WasmBinary.Unsigned(code, used, field);
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, index);
            used = Const(code, used, k);
            code[used++] = 0x6c; // i32.mul
            return Local(code, used, 0x21, target);
        }

        // [base] -> [value] (load) or [base, value] -> [] (store) of E field `ordinal`.
        internal static int EmitAccess(byte[] code, int used, int[] objects, int owner, int field, int ordinal,
            int baseIndex, int value, bool store, int storage)
        {
            int vector = storage == I64 ? Longs : Ints;
            used = Local(code, used, 0x20, 0);
            used = Gc(code, used, 2, owner);
            used = WasmBinary.Unsigned(code, used, field);
            used = Local(code, used, 0x20, baseIndex);
            if (ordinal != 0)
            {
                used = Const(code, used, ordinal);
                code[used++] = 0x6a;
            }
            if (!store) return Gc(code, used, 0x0b, vector); // array.get
            used = Local(code, used, 0x20, value);
            return Gc(code, used, 0x0e, vector); // array.set
        }

        // The class field index of the method's flat field (FlatIndex aux), or -1.
        internal static int Field(int[] records)
        {
            for (int at = 0; at < records.Length; at += Width)
                if (records[at] == FlatIndex) return records[at + 3] & 0xffff;
            return -1;
        }
    }
}
