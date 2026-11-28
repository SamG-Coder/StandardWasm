using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: rank-2, zero-based arrays of Int32/UInt32 or Int64/UInt64 elements
    // (`T[,]`, ELEMENT_TYPE_ARRAY with rank 2 and zero lower bounds).
    //
    // Representation: the element type's ordinary Wasm vector of length rows * cols + 2, with
    // [0] = rows, [1] = cols and element (i, j) at 2 + i * cols + j. IL reaches an ARRAY only
    // through its runtime members (the constructor, Get, Set, Address), never through the
    // SZARRAY instructions, so sharing the vector type cannot mix the two kinds.
    //   newobj T[,]::.ctor(int32, int32) with RuntimeHelpers.InitializeArray: the module data
    //     segment receives rows, cols and then the RVA elements (ArrayInitializers).
    //   T T[,]::Get(int32, int32): each index is checked against its own dimension and raises
    //     IndexOutOfRangeException, as on the CLR.
    internal static class MdArrays
    {
        internal const int Capacity = 120;

        // The vector of an ELEMENT_TYPE_ARRAY signature at next[0] (just after 0x14), or < 0.
        internal static int ReadArraySignature(byte[] image, int[] next, int end)
        {
            int at = next[0];
            if (at >= end) return -1;
            int element = image[at++];
            int vector = element == 0x08 || element == 0x09 ? Ints : element == 0x0a || element == 0x0b ? Longs : 0;
            var cursor = new int[1];
            int rank = CompressedUnsigned(image, at, end, cursor);
            if (rank != 2) return -2;
            int sizes = CompressedUnsigned(image, cursor[0], end, cursor);
            if (sizes != 0) return -2;
            int bounds = CompressedUnsigned(image, cursor[0], end, cursor);
            for (int i = 0; i < bounds; i++)
                if (CompressedUnsigned(image, cursor[0], end, cursor) != 0) return -2; // zero lower bounds only
            if (bounds < 0 || vector == 0) return -2;
            next[0] = cursor[0];
            return vector;
        }

        // The vector of the rank-2 array type a MemberRef's TypeSpec parent names, or 0.
        internal static int MemberVector(byte[] image, int[] heaps, int[] layout, int member)
        {
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, member);
            if (parent < 0 || (parent & 7) != 4) return 0; // MemberRefParent tag 4: TypeSpec
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, parent >> 3);
            if (blob < 0) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (size < 2 || size > limit - next[0] || image[next[0]] != 0x14) return 0;
            int end = next[0] + size;
            next[0]++;
            int vector = ReadArraySignature(image, next, end);
            return vector > 0 && next[0] == end ? vector : 0;
        }

        // 1 for `.ctor(int32, int32)`, 2 for `T Get(int32, int32)`, otherwise 0.
        internal static int Member(byte[] image, int[] heaps, int[] layout, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 &&
                MemberName(image, heaps, layout, member, reference) < 0) return 0;
            uint name = (uint)reference[4];
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 6, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            // instance void .ctor(int32, int32): length 5
            if (MatchesAscii(image, heaps, name, ".ctor"))
                return U32(image, signature) == 0x01022005 && U16(image, signature + 4) == 0x0808 ? 1 : 0;
            // instance T Get(int32, int32): length 5, return Int32/UInt32/Int64/UInt64
            if (MatchesAscii(image, heaps, name, "Get") && U16(image, signature) == 0x2005 &&
                image[signature + 2] == 2 && U16(image, signature + 4) == 0x0808)
            {
                int result = image[signature + 3];
                return result >= 0x08 && result <= 0x0b ? 2 : 0;
            }
            return 0;
        }

        // Fills the name and signature of a MemberRef whose parent is a TypeSpec.
        private static int MemberName(byte[] image, int[] heaps, int[] layout, int row, int[] reference)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return -1;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 10);
            if (table < 0) return -1;
            int strings = layout[2], blobs = layout[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(table + (row - 1) * width);
            int parentWidth = width - strings - blobs;
            reference[4] = (int)(strings == 2 ? U16(image, at + (uint)parentWidth) : U32(image, at + (uint)parentWidth));
            reference[5] = (int)(blobs == 2 ? U16(image, at + (uint)(parentWidth + strings)) : U32(image, at + (uint)(parentWidth + strings)));
            return 0;
        }

        // [array, i, j] -> [element]
        internal static int EmitGet(byte[] code, int used, int vector, int array, int row, int column)
        {
            bool wide = vector == Longs;
            used = BranchWasm.GuardNull(code, used, array);
            // (uint)i >= rows || (uint)j >= cols -> IndexOutOfRangeException
            used = BranchWasm.Local(code, used, 0x20, row);
            used = Dimension(code, used, vector, array, 0);
            code[used++] = 0x4f; // i32.ge_u
            used = BranchWasm.Local(code, used, 0x20, column);
            used = Dimension(code, used, vector, array, 1);
            code[used++] = 0x4f;
            code[used++] = 0x72; // i32.or
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
            used = BranchWasm.Local(code, used, 0x20, array);
            used = BranchWasm.Local(code, used, 0x20, row);
            used = Dimension(code, used, vector, array, 1);
            code[used++] = 0x6c; // i32.mul
            used = BranchWasm.Local(code, used, 0x20, column);
            code[used++] = 0x6a;
            used = BranchWasm.Const(code, used, 2);
            code[used++] = 0x6a;
            return BranchWasm.Gc(code, used, 0x0b, wide ? Longs : Ints); // array.get
        }

        // [] -> [dimension as i32]
        private static int Dimension(byte[] code, int used, int vector, int array, int index)
        {
            used = BranchWasm.Local(code, used, 0x20, array);
            used = BranchWasm.Const(code, used, index);
            used = BranchWasm.Gc(code, used, 0x0b, vector);
            if (vector == Longs) code[used++] = 0xa7; // i32.wrap_i64
            return used;
        }
    }
}
