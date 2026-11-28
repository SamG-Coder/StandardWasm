using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Decision 0105: `int Array.BinarySearch<T>(T[] array, T value)` for a 32/64-bit integer T,
    // with the CLR's exact probe order (ArraySortHelper<T>.BinarySearch), so the index returned
    // among equal elements matches:
    //   lo = 0, hi = length - 1; while (lo <= hi) { i = lo + ((hi - lo) >> 1); compare a[i] with
    //   value: equal -> i; less -> lo = i + 1; greater -> hi = i - 1 }; return ~lo.
    // A null array raises ArgumentNullException.
    internal static class ArrayBinarySearch
    {
        internal const int Capacity = 160;

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records)
        {
            int[] counts = null;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != MethodSpecCall || records[slot + 3] != 0) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                }
                var spec = new int[MethodSpecifications.Width];
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[slot + 1], spec) < 0 ||
                    spec[MethodSpecifications.Arity] != 1 ||
                    spec[MethodSpecifications.Arguments] + 1 != spec[MethodSpecifications.End]) continue;
                int element = image[spec[MethodSpecifications.Arguments]];
                if (element < 0x08 || element > 0x0b) continue;
                int target = spec[MethodSpecifications.Target];
                if ((target & 1) == 0) continue;
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "BinarySearch") ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                    continue;
                int blob = member[5];
                if (blob < 1 || !Fits((uint)blob, 10, (uint)heaps[1])) continue;
                uint s = (uint)(heaps[0] + blob);
                // int32 BinarySearch<T>(!!0[], !!0): 09 10 01 02 08 1d 1e 00 1e 00
                if (image[s] != 9 || image[s + 1] != 0x10 || image[s + 2] != 1 || image[s + 3] != 2 ||
                    image[s + 4] != 0x08 || image[s + 5] != 0x1d || image[s + 6] != 0x1e || image[s + 7] != 0 ||
                    image[s + 8] != 0x1e || image[s + 9] != 0) continue;
                records[slot] = BinarySearchPrimitiveArray;
                records[slot + 1] = element;
                records[slot + 3] = 0;
            }
        }

        internal static int Vector(int element) => ArraySortPrimitive.Vector(element);

        internal static int ValueType(int element) => element >= 0x0a ? SignatureTypes.I64 : SignatureTypes.I32;

        // [array, value] -> [index]. `lo`, `hi` and `mid` are i32 scratch locals; `result` is the i32 spill.
        internal static int Emit(byte[] code, int used, int element, int array, int value, int lo, int hi, int mid, int result)
        {
            int vector = Vector(element);
            bool wide = element >= 0x0a, signed = (element & 1) == 0;
            byte equal = wide ? (byte)0x51 : (byte)0x46;
            byte less = wide ? (signed ? (byte)0x53 : (byte)0x54) : (signed ? (byte)0x48 : (byte)0x49);
            used = GuardNull(code, used, array, ExceptionTagsWasm.ArgumentNullTag);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, lo);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x21, hi);
            code[used++] = 0x02; code[used++] = 0x40; // block: found (result already set)
            code[used++] = 0x02; code[used++] = 0x40; // block: missed
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, lo);
            used = Local(code, used, 0x20, hi);
            code[used++] = 0x4a; // i32.gt_s
            code[used++] = 0x0d; code[used++] = 1; // br_if missed
            // mid = lo + ((hi - lo) >> 1)
            used = Local(code, used, 0x20, lo);
            used = Local(code, used, 0x20, hi);
            used = Local(code, used, 0x20, lo);
            code[used++] = 0x6b;
            used = Const(code, used, 1);
            code[used++] = 0x75; // i32.shr_s
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, mid);
            // a[mid] == value -> found
            used = Element(code, used, vector, array, mid);
            used = Local(code, used, 0x20, value);
            code[used++] = equal;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, mid);
            used = Local(code, used, 0x21, result);
            code[used++] = 0x0c; code[used++] = 3; // br found
            code[used++] = 0x0b;
            // a[mid] < value -> lo = mid + 1, else hi = mid - 1
            used = Element(code, used, vector, array, mid);
            used = Local(code, used, 0x20, value);
            code[used++] = less;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, mid);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, lo);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, mid);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x21, hi);
            code[used++] = 0x0b;
            code[used++] = 0x0c; code[used++] = 0; // continue
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // missed
            used = Local(code, used, 0x20, lo);
            used = Const(code, used, -1);
            code[used++] = 0x73; // i32.xor: ~lo
            used = Local(code, used, 0x21, result);
            code[used++] = 0x0b; // found
            return used;
        }

        private static int Element(byte[] code, int used, int vector, int array, int index)
        {
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, index);
            return Gc(code, used, 0x0b, vector); // array.get
        }
    }
}
