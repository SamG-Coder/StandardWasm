using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ArrayCopyWasm
    {
        internal const int Capacity = 384;

        internal static int Vector(int[] objects, int source, int destination)
        {
            if (source != NullValue && destination != NullValue && source != destination) return 0;
            int vector = source != NullValue ? source : destination;
            int element = ArrayElement(objects, vector);
            if (element != 0) return vector;
            // Some packed heaps erase CLI distinctions (e.g. bool and byte).
            // Require a known simple-copy pair or an exact nominal vector.
            return vector == Ints || vector == Longs || vector == NativeVector ||
                vector == Singles || vector == Doubles ? vector : 0;
        }

        internal static int Emit(byte[] code, int used, int vector, int source, int sourceIndex,
            int destination, int destinationIndex, int length, bool aggregate, bool tracksRoots,
            int cursor, int direction, int snapshot)
        {
            used = CheckNull(code, used, source);
            used = CheckNull(code, used, destination);
            used = CheckNonNegative(code, used, length);
            used = CheckRange(code, used, source, sourceIndex, length);
            used = CheckRange(code, used, destination, destinationIndex, length);
            if (aggregate)
                return AggregateArrayCopyWasm.Emit(code, used, vector, source, sourceIndex, destination,
                    destinationIndex, length, cursor, direction, snapshot, tracksRoots);

            used = Local(code, used, 0x20, destination);
            used = LoadIndex(code, used, destinationIndex);
            used = Local(code, used, 0x20, source);
            used = LoadIndex(code, used, sourceIndex);
            used = Local(code, used, 0x20, length);
            used = Gc(code, used, 0x11, vector); // array.copy handles overlapping ranges
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType(vector));
        }

        private static int CheckNull(byte[] code, int used, int array)
        {
            used = Local(code, used, 0x20, array);
            code[used++] = 0xd1;
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentNullTag);
        }

        private static int CheckNonNegative(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
        }

        private static int CheckRange(byte[] code, int used, int array, int index, int length)
        {
            if (index >= 0) used = CheckNonNegative(code, used, index);
            used = LoadIndex(code, used, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            // The sum is two nonnegative Int32 values and fits in uint32.
            code[used++] = 0x4b;
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
        }

        internal static int LoadIndex(byte[] code, int used, int index) =>
            index < 0 ? Const(code, used, 0) : Local(code, used, 0x20, index);
    }
}
