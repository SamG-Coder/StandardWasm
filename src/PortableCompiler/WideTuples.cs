using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: System.UInt128 values, ValueTuple<UInt128, int> values and arrays of those
    // tuples, for the members the rtree benchmark's exact nearest-neighbour path uses.
    //
    // Representation: a UInt128 or a (UInt128, int) tuple is one immutable Wasm GC struct
    // {i64 lo, i64 hi, i32 extra} ($wide; extra is the tuple's Item2, 0 for a UInt128). The tuple
    // array is an array of nullable references to it ($wideArray); a null element is the CLR's
    // default tuple (0, 0), and every read treats it so.
    //
    // Members, exact for every input:
    //   - UInt128 op_Implicit(ulong), op_Addition and op_Multiply (unchecked, modulo 2^128);
    //   - new ValueTuple<UInt128, int>(a, b), newarr (OverflowException for a negative size),
    //     stelem and `ldelema; ldfld Item2` (NullReferenceException and IndexOutOfRangeException);
    //   - Array.Sort<(UInt128, int)>(array, index, length) by the default comparer: Item1 as an
    //     unsigned 128-bit value, then Item2 as a signed int. Equal tuples cannot be told apart,
    //     so the heap sort here produces the same array as the CLR's introsort. Argument checks
    //     follow Array.Sort: null -> ArgumentNullException; a negative index or length ->
    //     ArgumentOutOfRangeException; array.Length - index < length -> ArgumentException.
    internal static class WideTuples
    {
        internal const int Value = 46400, Tuple = 46401, TupleArray = 46402;
        internal const int FromUInt64 = 1, AddValues = 2, MultiplyValues = 3, NewTuple = 4, NewTupleArray = 5,
            StoreElement = 6, ElementItem2 = 7, SortRange = 8;
        internal static int Capacity(int op) => op == SortRange ? 1400 : op == MultiplyValues ? 420 : 160;

        internal static bool IsWide(int type) => type == Value || type == Tuple;

        // ---- signatures ------------------------------------------------------------------------

        internal static bool IsUInt128(byte[] image, int[] layout, int[] heaps, int coded) =>
            SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "UInt128");

        internal static int Instantiation(byte[] image, int[] layout, int[] heaps, int coded, int[] arguments, int count) =>
            count == 2 && arguments[0] == Value && arguments[1] == GenericScalar.Key(0x08) &&
            SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "ValueTuple`2") ? Tuple : 0;

        // ---- identification --------------------------------------------------------------------

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            int[] counts = null;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                int op = 0;
                if (kind == MemberCall && (records[slot + 3] == 0x28 || records[slot + 3] == 0x73))
                    op = Member(image, layout, heaps, records[slot + 1], records[slot + 3]);
                else if (kind == NewArray)
                    op = IsTupleToken(image, layout, heaps, records[slot + 1]) ? NewTupleArray : 0;
                else if (kind == ArraySet && records[slot + 1] == 0) // stelem <token>: the token is in aux
                    op = IsTupleToken(image, layout, heaps, records[slot + 3]) ? StoreElement : 0;
                else if (kind == ObjectElementAddress && IsTupleCoded(image, layout, heaps, records[slot + 1]))
                {
                    // ldelema (UInt128, int); ldfld Item2 -> one element read.
                    int next = records[slot + 2] * Width;
                    if (next < records.Length && records[next] == GetField && records[next + 3] == 1 &&
                        !ValueOperands.Entry(records, next / Width, clauses) &&
                        IsTupleItem2(image, layout, heaps, records[next + 1]))
                    {
                        records[next] = Nop;
                        op = ElementItem2;
                    }
                }
                else if (kind == MethodSpecCall && records[slot + 3] == 0)
                {
                    if (counts == null)
                    {
                        counts = new int[45];
                        if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                    }
                    op = IsSort(image, layout, heaps, counts, records[slot + 1]) ? SortRange : 0;
                }
                if (op == 0) continue;
                records[slot] = WideOp;
                records[slot + 1] = op;
                records[slot + 3] = 0;
            }
        }

        private static int Member(byte[] image, int[] layout, int[] heaps, int row, int opcode)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member, allowClosedParent: true) < 0) return 0;
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, row);
            if (parent < 0) return 0;
            uint name = (uint)member[4];
            int blob = member[5];
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1])) return 0;
            uint s = (uint)(heaps[0] + blob);
            if ((parent & 7) == 4)
            {
                // newobj ValueTuple`2<UInt128, int32>::.ctor(!0, !1)
                return opcode == 0x73 && IsTupleSpec(image, layout, heaps, parent >> 3) &&
                    MatchesAscii(image, heaps, name, ".ctor") && image[s + 1] == 0x20 && image[s + 2] == 2 ? NewTuple : 0;
            }
            if ((parent & 7) != 1 || opcode != 0x28 || !IsUInt128(image, layout, heaps, (parent >> 3) << 2 | 1) ||
                image[s + 1] != 0x00) return 0;
            int parameters = image[s + 2];
            if (parameters == 1 && MatchesAscii(image, heaps, name, "op_Implicit") && image[s + 3 + Extent(image, s + 3)] == 0x0b)
                return FromUInt64;
            if (parameters == 2 && MatchesAscii(image, heaps, name, "op_Addition")) return AddValues;
            if (parameters == 2 && MatchesAscii(image, heaps, name, "op_Multiply")) return MultiplyValues;
            return 0;
        }

        // The encoded size of `valuetype UInt128` (11 <coded>) at `at`.
        private static int Extent(byte[] image, uint at)
        {
            if (image[at] != 0x11) return 1;
            return 1 + ((image[at + 1] & 0x80) == 0 ? 1 : (image[at + 1] & 0xc0) == 0x80 ? 2 : 4);
        }

        private static bool IsTupleSpec(byte[] image, int[] layout, int[] heaps, int row)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob <= 0) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0], end = at + size;
            if (size < 6 || size > limit - at || image[at] != 0x15 || image[at + 1] != 0x11) return false;
            int tuple = CompressedUnsigned(image, at + 2, end, next);
            if (tuple < 0 || !SpanMetadata.IsPlatformType(image, layout, heaps, tuple, "System", "ValueTuple`2")) return false;
            at = next[0];
            if (at + 3 > end || image[at] != 2 || image[at + 1] != 0x11) return false;
            int item = CompressedUnsigned(image, at + 2, end, next);
            return item >= 0 && IsUInt128(image, layout, heaps, item) && next[0] + 1 == end && image[next[0]] == 0x08;
        }

        private static bool IsTupleToken(byte[] image, int[] layout, int[] heaps, int token) =>
            (uint)token >> 24 == 0x1b && IsTupleSpec(image, layout, heaps, token & 0xffffff);

        private static bool IsTupleCoded(byte[] image, int[] layout, int[] heaps, int coded) =>
            (coded & 3) == 2 && IsTupleSpec(image, layout, heaps, coded >> 2);

        private static bool IsTupleItem2(byte[] image, int[] layout, int[] heaps, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member, allowClosedParent: true) < 0) return false;
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, row);
            return parent >= 0 && (parent & 7) == 4 && IsTupleSpec(image, layout, heaps, parent >> 3) &&
                MatchesAscii(image, heaps, (uint)member[4], "Item2");
        }

        // void Array.Sort<(UInt128, int)>(!!0[], int32, int32)
        private static bool IsSort(byte[] image, int[] layout, int[] heaps, int[] counts, int row)
        {
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0 ||
                spec[MethodSpecifications.Arity] != 1) return false;
            int at = spec[MethodSpecifications.Arguments], end = spec[MethodSpecifications.End];
            if (end - at < 4 || image[at] != 0x15 || image[at + 1] != 0x11) return false;
            var next = new int[1];
            int tuple = CompressedUnsigned(image, at + 2, end, next);
            if (tuple < 0 || !SpanMetadata.IsPlatformType(image, layout, heaps, tuple, "System", "ValueTuple`2")) return false;
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) == 0) return false;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "Sort") ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                return false;
            // The instantiation must be exactly (UInt128, int32).
            at = next[0];
            if (end - at < 4 || image[at] != 2 || image[at + 1] != 0x11) return false;
            int item = CompressedUnsigned(image, at + 2, end, next);
            if (item < 0 || !IsUInt128(image, layout, heaps, item) || next[0] + 1 != end || image[next[0]] != 0x08) return false;
            int blob = member[5];
            if (blob < 1 || !Fits((uint)blob, 10, (uint)heaps[1])) return false;
            uint s = (uint)(heaps[0] + blob);
            // void Sort<T>(!!0[], int32, int32): 09 10 01 03 01 1d 1e 00 08 08
            return image[s] == 9 && image[s + 1] == 0x10 && image[s + 2] == 1 && image[s + 3] == 3 && image[s + 4] == 1 &&
                image[s + 5] == 0x1d && image[s + 6] == 0x1e && image[s + 7] == 0 && image[s + 8] == 0x08 && image[s + 9] == 0x08;
        }

        // ---- verification ----------------------------------------------------------------------

        internal static int Arity(int op) =>
            op == FromUInt64 || op == NewTupleArray ? 1 : op == StoreElement || op == SortRange ? 3 : 2;

        internal static int Input(int op, int index) =>
            op == FromUInt64 ? I64 :
            op == AddValues || op == MultiplyValues ? Value :
            op == NewTuple ? (index == 0 ? Value : I32) :
            op == NewTupleArray ? I32 :
            op == StoreElement ? (index == 0 ? TupleArray : index == 1 ? I32 : Tuple) :
            op == ElementItem2 ? (index == 0 ? TupleArray : I32) :
            index == 0 ? TupleArray : I32;

        internal static int Result(int op) =>
            op == FromUInt64 || op == AddValues || op == MultiplyValues ? Value :
            op == NewTuple ? Tuple : op == NewTupleArray ? TupleArray : op == ElementItem2 ? I32 : Void;

        // ---- emission --------------------------------------------------------------------------

        private const int WideType = Value; // heap marker mapped by WasmBinary

        // [field of a $wide ref, or 0 for null] -> [value]
        private static int Field(byte[] code, int used, int local, int field)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            code[used++] = field == 2 ? (byte)0x7f : (byte)0x7e;
            if (field == 2) used = Const(code, used, 0);
            else { code[used++] = 0x42; code[used++] = 0; }
            code[used++] = 0x05;
            used = Local(code, used, 0x20, local);
            used = Gc(code, used, 2, WideType);
            code[used++] = (byte)field;
            code[used++] = 0x0b;
            return used;
        }

        // operands: spill locals (deepest first). i32 scratch s[0..4]; i64 scratch w[0..3];
        // wideScratch: two $wide locals.
        internal static int Emit(byte[] code, int used, int op, int[] operands, int target, int[] s, int[] w, int[] wideScratch)
        {
            switch (op)
            {
                case FromUInt64:
                    used = Local(code, used, 0x20, operands[0]);
                    code[used++] = 0x42; code[used++] = 0; // hi
                    used = Const(code, used, 0);
                    used = Gc(code, used, 0, WideType);
                    return Local(code, used, 0x21, target);
                case AddValues:
                    // lo = a.lo + b.lo; hi = a.hi + b.hi + (lo <u a.lo)
                    used = Field(code, used, operands[0], 0);
                    used = Field(code, used, operands[1], 0);
                    code[used++] = 0x7c;
                    used = Local(code, used, 0x22, w[0]);
                    used = Field(code, used, operands[0], 1);
                    used = Field(code, used, operands[1], 1);
                    code[used++] = 0x7c;
                    used = Local(code, used, 0x20, w[0]);
                    used = Field(code, used, operands[0], 0);
                    code[used++] = 0x54; // i64.lt_u: carry
                    code[used++] = 0xad;
                    code[used++] = 0x7c;
                    used = Const(code, used, 0);
                    used = Gc(code, used, 0, WideType);
                    return Local(code, used, 0x21, target);
                case MultiplyValues:
                    // lo = al * bl; hi = mulhi_u(al, bl) + al * bh + ah * bl (all modulo 2^64)
                    used = Field(code, used, operands[0], 0); used = Local(code, used, 0x21, w[0]);
                    used = Field(code, used, operands[1], 0); used = Local(code, used, 0x21, w[1]);
                    used = Field(code, used, operands[0], 1); used = Local(code, used, 0x21, w[2]);
                    used = Field(code, used, operands[1], 1); used = Local(code, used, 0x21, w[3]);
                    used = Local(code, used, 0x20, w[0]);
                    used = Local(code, used, 0x20, w[1]);
                    code[used++] = 0x7e; // lo
                    used = WideMultiplyDivide.High(code, used, w[0], w[1]);
                    used = Local(code, used, 0x20, w[0]);
                    used = Local(code, used, 0x20, w[3]);
                    code[used++] = 0x7e;
                    code[used++] = 0x7c;
                    used = Local(code, used, 0x20, w[2]);
                    used = Local(code, used, 0x20, w[1]);
                    code[used++] = 0x7e;
                    code[used++] = 0x7c; // hi
                    used = Const(code, used, 0);
                    used = Gc(code, used, 0, WideType);
                    return Local(code, used, 0x21, target);
                case NewTuple:
                    used = Field(code, used, operands[0], 0);
                    used = Field(code, used, operands[0], 1);
                    used = Local(code, used, 0x20, operands[1]);
                    used = Gc(code, used, 0, WideType);
                    return Local(code, used, 0x21, target);
                case NewTupleArray:
                    used = Local(code, used, 0x20, operands[0]);
                    used = Const(code, used, 0);
                    code[used++] = 0x48; // i32.lt_s: a negative size
                    used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.OverflowTag);
                    used = Local(code, used, 0x20, operands[0]);
                    used = Gc(code, used, 0x07, TupleArray); // array.new_default: (0, 0) tuples
                    return Local(code, used, 0x21, target);
                case StoreElement:
                    used = Bounds(code, used, operands[0], operands[1]);
                    used = Local(code, used, 0x20, operands[0]);
                    used = Local(code, used, 0x20, operands[1]);
                    used = Local(code, used, 0x20, operands[2]);
                    return Gc(code, used, 0x0e, TupleArray); // array.set
                case ElementItem2:
                    used = Bounds(code, used, operands[0], operands[1]);
                    used = Local(code, used, 0x20, operands[0]);
                    used = Local(code, used, 0x20, operands[1]);
                    used = Gc(code, used, 0x0b, TupleArray); // array.get
                    used = Local(code, used, 0x21, wideScratch[0]);
                    used = Field(code, used, wideScratch[0], 2);
                    return Local(code, used, 0x21, target);
                case SortRange:
                    return Sort(code, used, operands[0], operands[1], operands[2], s, wideScratch);
            }
            return used;
        }

        // null array -> NullReferenceException; (uint)index >= length -> IndexOutOfRangeException
        private static int Bounds(byte[] code, int used, int array, int index)
        {
            used = GuardNull(code, used, array);
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f;
            code[used++] = 0x4f; // i32.ge_u
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
        }

        // [] -> [x < y] for two $wide locals: Item1 unsigned 128-bit, then Item2 signed.
        private static int Less(byte[] code, int used, int x, int y)
        {
            // x.hi <u y.hi || x.hi == y.hi && (x.lo <u y.lo || x.lo == y.lo && x.extra <s y.extra)
            used = Field(code, used, x, 1);
            used = Field(code, used, y, 1);
            code[used++] = 0x54; // i64.lt_u
            used = Field(code, used, x, 1);
            used = Field(code, used, y, 1);
            code[used++] = 0x51; // i64.eq
            used = Field(code, used, x, 0);
            used = Field(code, used, y, 0);
            code[used++] = 0x54;
            used = Field(code, used, x, 0);
            used = Field(code, used, y, 0);
            code[used++] = 0x51;
            used = Field(code, used, x, 2);
            used = Field(code, used, y, 2);
            code[used++] = 0x48; // i32.lt_s
            code[used++] = 0x71; // and
            code[used++] = 0x72; // or
            code[used++] = 0x71; // and
            code[used++] = 0x72; // or
            return used;
        }

        // Heap sort of array[index .. index + length). s: n(base), start, end, root, child.
        private static int Sort(byte[] code, int used, int array, int index, int length, int[] s, int[] refs)
        {
            int start = s[1], end = s[2], root = s[3], child = s[4], baseIndex = s[0];
            int x = refs[0], y = refs[1];
            used = GuardNull(code, used, array, ExceptionTagsWasm.ArgumentNullTag);
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x72; // or: either negative
            used = Const(code, used, 0);
            code[used++] = 0x48;
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f;
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6b;
            used = Local(code, used, 0x20, length);
            code[used++] = 0x48; // length - index < count
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x21, baseIndex);
            used = Local(code, used, 0x20, length);
            used = Local(code, used, 0x22, end);
            used = Const(code, used, 1);
            code[used++] = 0x76; // shr_u
            used = Local(code, used, 0x21, start);
            code[used++] = 0x02; code[used++] = 0x40; // block: done
            code[used++] = 0x03; code[used++] = 0x40; // loop: next root
            used = Local(code, used, 0x20, start);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, start);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, start);
            used = Local(code, used, 0x21, root);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, end);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, end);
            used = Const(code, used, 0);
            code[used++] = 0x4c; // i32.le_s
            code[used++] = 0x0d; code[used++] = 2; // br_if done
            used = Swap(code, used, array, baseIndex, -1, end, x, y);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, root);
            code[used++] = 0x0b;
            code[used++] = 0x02; code[used++] = 0x40; // block: sifted
            code[used++] = 0x03; code[used++] = 0x40;
            used = Local(code, used, 0x20, root);
            used = Const(code, used, 1);
            code[used++] = 0x74;
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x22, child);
            used = Local(code, used, 0x20, end);
            code[used++] = 0x4f;
            code[used++] = 0x0d; code[used++] = 1;
            // if (child + 1 < end && a[child] < a[child + 1]) child++
            used = Local(code, used, 0x20, child);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, end);
            code[used++] = 0x49;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, array, baseIndex, child, 0, x);
            used = Get(code, used, array, baseIndex, child, 1, y);
            used = Less(code, used, x, y);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, child);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, child);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            // if (!(a[root] < a[child])) break; swap(root, child); root = child
            used = Get(code, used, array, baseIndex, root, 0, x);
            used = Get(code, used, array, baseIndex, child, 0, y);
            used = Less(code, used, x, y);
            code[used++] = 0x45;
            code[used++] = 0x0d; code[used++] = 1;
            used = Swap(code, used, array, baseIndex, root, child, x, y);
            used = Local(code, used, 0x20, child);
            used = Local(code, used, 0x21, root);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            code[used++] = 0x0c; code[used++] = 0; // next root
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        // target = array[base + index + offset]
        private static int Get(byte[] code, int used, int array, int baseIndex, int index, int offset, int target)
        {
            used = Local(code, used, 0x20, array);
            used = Position(code, used, baseIndex, index, offset);
            used = Gc(code, used, 0x0b, TupleArray);
            return Local(code, used, 0x21, target);
        }

        private static int Position(byte[] code, int used, int baseIndex, int index, int offset)
        {
            used = Local(code, used, 0x20, baseIndex);
            if (index >= 0)
            {
                used = Local(code, used, 0x20, index);
                code[used++] = 0x6a;
            }
            if (offset != 0)
            {
                used = Const(code, used, offset);
                code[used++] = 0x6a;
            }
            return used;
        }

        // swap array[base + i] and array[base + j]; i < 0 names element base + 0.
        private static int Swap(byte[] code, int used, int array, int baseIndex, int i, int j, int x, int y)
        {
            used = Get(code, used, array, baseIndex, i, 0, x);
            used = Get(code, used, array, baseIndex, j, 0, y);
            used = Local(code, used, 0x20, array);
            used = Position(code, used, baseIndex, i, 0);
            used = Local(code, used, 0x20, y);
            used = Gc(code, used, 0x0e, TupleArray);
            used = Local(code, used, 0x20, array);
            used = Position(code, used, baseIndex, j, 0);
            used = Local(code, used, 0x20, x);
            return Gc(code, used, 0x0e, TupleArray);
        }

        // ---- module types ----------------------------------------------------------------------

        internal const int TypeBytes = 2 + 3 * 2 + 4;

        internal static int WriteTypes(byte[] output, int cursor, int wideHeap)
        {
            output[cursor++] = 0x5f; // struct {i64 lo, i64 hi, i32 extra}, immutable
            output[cursor++] = 3;
            output[cursor++] = 0x7e; output[cursor++] = 0;
            output[cursor++] = 0x7e; output[cursor++] = 0;
            output[cursor++] = 0x7f; output[cursor++] = 0;
            output[cursor++] = 0x5e; // array (mut (ref null $wide))
            output[cursor++] = 0x63;
            output[cursor++] = (byte)wideHeap;
            output[cursor++] = 1;
            return cursor;
        }
    }
}
