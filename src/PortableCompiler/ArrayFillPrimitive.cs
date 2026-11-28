using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: `Array.Fill<T>(T[] array, T value)` and
    // `Array.Fill<T>(T[] array, T value, int startIndex, int count)` for a primitive T, as one
    // Wasm GC `array.fill`. The CLR's argument checks come first, in its order:
    //   array == null                         -> ArgumentNullException
    //   (uint)startIndex > (uint)array.Length -> ArgumentOutOfRangeException
    //   (uint)count > (uint)(Length - start)  -> ArgumentOutOfRangeException
    // The record operand is the element type, plus Ranged for the four-argument overload.
    internal static class ArrayFillPrimitive
    {
        internal const int Capacity = 96, Ranged = 0x100;

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
                if (PinnedArrays.Vector(element) == 0) continue;
                int target = spec[MethodSpecifications.Target];
                if ((target & 1) == 0) continue;
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "Fill") ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                    continue;
                int blob = member[5];
                if (blob < 1 || !Fits((uint)blob, 12, (uint)heaps[1])) continue;
                uint s = (uint)(heaps[0] + blob);
                // void Fill<T>(!!0[], !!0):               09 10 01 02 01 1d 1e 00 1e 00
                // void Fill<T>(!!0[], !!0, int32, int32): 0b 10 01 04 01 1d 1e 00 1e 00 08 08
                int parameters = image[s + 3];
                if (image[s + 1] != 0x10 || image[s + 2] != 1 || image[s + 4] != 1 ||
                    image[s + 5] != 0x1d || image[s + 6] != 0x1e || image[s + 7] != 0 ||
                    image[s + 8] != 0x1e || image[s + 9] != 0) continue;
                bool ranged = parameters == 4 && image[s] == 11 && image[s + 10] == 0x08 && image[s + 11] == 0x08;
                if (!ranged && (parameters != 2 || image[s] != 9)) continue;
                records[slot] = FillPrimitiveArray;
                records[slot + 1] = element | (ranged ? Ranged : 0);
                records[slot + 3] = 0;
            }
        }

        internal static int Vector(int operand) => PinnedArrays.Vector(operand & 0xff);

        internal static int Arity(int operand) => (operand & Ranged) != 0 ? 4 : 2;

        // The stack type of the fill value.
        internal static int ValueType(int operand)
        {
            int vector = Vector(operand);
            return vector == Longs ? I64 : vector == Singles ? F32 : vector == Doubles ? F64 : I32;
        }

        // [array, value(, start, count)] -> []
        internal static int Emit(byte[] code, int used, int operand, int array, int value, int start, int count)
        {
            int vector = Vector(operand);
            bool ranged = (operand & Ranged) != 0;
            used = GuardNull(code, used, array, ExceptionTagsWasm.ArgumentNullTag);
            if (ranged)
            {
                used = Local(code, used, 0x20, start);
                used = Local(code, used, 0x20, array);
                code[used++] = 0xfb; code[used++] = 0x0f; // array.len
                code[used++] = 0x4b; // i32.gt_u
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
                used = Local(code, used, 0x20, count);
                used = Local(code, used, 0x20, array);
                code[used++] = 0xfb; code[used++] = 0x0f;
                used = Local(code, used, 0x20, start);
                code[used++] = 0x6b; // i32.sub
                code[used++] = 0x4b;
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            }
            used = Local(code, used, 0x20, array);
            if (ranged) used = Local(code, used, 0x20, start);
            else used = Const(code, used, 0);
            used = Local(code, used, 0x20, value);
            if (ranged) used = Local(code, used, 0x20, count);
            else
            {
                used = Local(code, used, 0x20, array);
                code[used++] = 0xfb; code[used++] = 0x0f;
            }
            return Gc(code, used, 0x10, vector); // array.fill
        }
    }
}
