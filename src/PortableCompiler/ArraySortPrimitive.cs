using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0104: System.Array.Sort<T>(T[]) for T = Int32, UInt32, Int64 or UInt64.
    //
    // For integers the sorted array is fully determined: equal keys are indistinguishable, so an
    // in-place heapsort (O(n log n) worst case, no allocation) yields exactly the CLR's result for
    // its introsort. A null array throws ArgumentNullException. Floating-point and other element
    // types stay diagnosed (their NaN ordering and comparers need the CoreLib implementation).
    internal static class ArraySortPrimitive
    {
        internal const int Capacity = 400;

        // Rewrites `call void Array::Sort<T>(T[])` MethodSpec calls in decoded records.
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
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "Sort") ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                    continue;
                // void Sort<T>(T[]): generic, arity 1, one parameter, void, SZARRAY MVAR 0
                int blob = member[5];
                if (blob < 1 || !Fits((uint)blob, 8, (uint)heaps[1])) continue;
                uint signature = (uint)(heaps[0] + blob);
                if (image[signature] != 7 || image[signature + 1] != 0x10 || image[signature + 2] != 1 ||
                    image[signature + 3] != 1 || image[signature + 4] != 1 || image[signature + 5] != 0x1d ||
                    image[signature + 6] != 0x1e || image[signature + 7] != 0) continue;
                records[slot] = SortPrimitiveArray;
                records[slot + 1] = element;
                records[slot + 3] = 0;
            }
        }

        internal static int Vector(int element) => element <= 0x09 ? Ints : Longs;

        // [array] -> []. `n`, `start`, `end`, `root` and `child` are i32 locals; `x` and `y` hold
        // elements (i32 for 32-bit, i64 for 64-bit elements).
        internal static int Emit(byte[] code, int used, int element, int array,
            int n, int start, int end, int root, int child, int x, int y)
        {
            int vector = Vector(element);
            bool wide = element >= 0x0a, signed = (element & 1) == 0;
            byte less = wide ? (signed ? (byte)0x53 : (byte)0x54) : (signed ? (byte)0x48 : (byte)0x49);

            used = GuardNull(code, used, array, ExceptionTagsWasm.ArgumentNullTag);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, n);
            used = Local(code, used, 0x21, end);
            used = Local(code, used, 0x20, n);
            used = Const(code, used, 1);
            code[used++] = 0x76; // i32.shr_u: start = n / 2
            used = Local(code, used, 0x21, start);

            code[used++] = 0x02; code[used++] = 0x40; // block: done
            code[used++] = 0x03; code[used++] = 0x40; // loop: next root
            // if (start > 0) root = --start; else { if (--end <= 0) break; swap(0, end); root = 0; }
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
            used = Swap(code, used, vector, array, -1, end, x, y);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, root);
            code[used++] = 0x0b;

            // Sift root down within [0, end).
            code[used++] = 0x02; code[used++] = 0x40; // block: sifted
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, root);
            used = Const(code, used, 1);
            code[used++] = 0x74; // i32.shl
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x22, child);
            used = Local(code, used, 0x20, end);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1; // br_if sifted
            // if (child + 1 < end && a[child] < a[child + 1]) child++;
            used = Local(code, used, 0x20, child);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, end);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, vector, array, child, 0);
            used = Get(code, used, vector, array, child, 1);
            code[used++] = less;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, child);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, child);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            // if (!(a[root] < a[child])) break; swap(root, child); root = child;
            used = Get(code, used, vector, array, root, 0);
            used = Get(code, used, vector, array, child, 0);
            code[used++] = less;
            code[used++] = 0x45;
            code[used++] = 0x0d; code[used++] = 1;
            used = Swap(code, used, vector, array, root, child, x, y);
            used = Local(code, used, 0x20, child);
            used = Local(code, used, 0x21, root);
            code[used++] = 0x0c; code[used++] = 0; // continue sifting
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block sifted
            code[used++] = 0x0c; code[used++] = 0; // next root
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block done
            return used;
        }

        // [] -> [a[index + offset]]; index -1 means element 0.
        private static int Get(byte[] code, int used, int vector, int array, int index, int offset)
        {
            used = Local(code, used, 0x20, array);
            if (index < 0) used = Const(code, used, 0);
            else
            {
                used = Local(code, used, 0x20, index);
                if (offset != 0)
                {
                    used = Const(code, used, offset);
                    code[used++] = 0x6a;
                }
            }
            return Gc(code, used, 0x0b, vector); // array.get
        }

        private static int Swap(byte[] code, int used, int vector, int array, int left, int right, int x, int y)
        {
            used = Get(code, used, vector, array, left, 0);
            used = Local(code, used, 0x21, x);
            used = Get(code, used, vector, array, right, 0);
            used = Local(code, used, 0x21, y);
            used = Set(code, used, vector, array, left, y);
            return Set(code, used, vector, array, right, x);
        }

        private static int Set(byte[] code, int used, int vector, int array, int index, int value)
        {
            used = Local(code, used, 0x20, array);
            used = index < 0 ? Const(code, used, 0) : Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, value);
            return Gc(code, used, 0x0e, vector); // array.set
        }
    }
}
