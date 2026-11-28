using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: System.Runtime.Intrinsics.Vector128<T>, Vector256<T> and Vector512<T> for
    // T = int, uint, long and ulong, with Wasm SIMD (v128).
    //
    // Representation: every vector value is one immutable Wasm GC struct of four v128 chunks
    // ($vector); Vector128 uses chunk 0 and Vector256 chunks 0-1, the rest stay zero. The
    // verifier keeps the CLI type as a tag (width and lane type), so only the reinterpreting
    // members (AsInt32, AsInt64) change it. A ValueTuple of two equal vector types, as
    // Vector512.Widen returns, is an immutable pair struct ($vectorPair). Vector locals start as
    // the all-zero vector, as CLI locals are zero-initialized. Vector-typed fields and arrays
    // stay diagnosed.
    //
    // Every member below is exact for every input:
    //   - comparisons return all-ones or zero lanes, as the CLR does;
    //   - ConditionalSelect(mask, a, b) is (a & mask) | (b & ~mask), v128.bitselect;
    //   - Min/Max of 64-bit lanes select with a signed compare (Wasm has no i64x2.min);
    //   - shifts mask the count to the lane width, as .NET 8+ does;
    //   - Shuffle returns zero for an index outside the vector, as the CLR does;
    //   - Vector128.GetElement raises ArgumentOutOfRangeException for an index outside [0, 4);
    //   - Load/Store are unaligned, like the CLR members; an out-of-memory address traps.
    internal static class SimdVectors
    {
        private const int TagBase = 46000, PairBase = 46200;
        internal static int Capacity(int op) => op == Shuffle ? 4200 : 760;

        internal const int Load = 1, Store = 2, Splat = 3, CreateLanes = 4, Select = 5, Equal = 6, Greater = 7,
            GreaterOrEqual = 8, Less = 9, LessOrEqual = 10, Max = 11, Min = 12, MostSignificantBits = 13,
            Indices = 14, Zero = 15, One = 16, And = 17, Or = 18, Add = 19, Subtract = 20, Multiply = 21,
            ShiftLeft = 22, ShiftRightLogical = 23, Abs = 24, AsInt32 = 25, AsInt64 = 26, Widen = 27,
            Shuffle = 28, LowerHalf = 29, UpperHalf = 30, ToScalar = 31, GetElement = 32, Item1 = 33, Item2 = 34,
            NewPair = 35, LoadIndirect = 36, StoreIndirect = 37;

        // ---- verifier tags -------------------------------------------------------------------

        internal static int Tag(int width, int lane) => TagBase + width * 16 + lane;
        internal static bool IsVector(int type) => type > TagBase && type < TagBase + 5 * 16;
        internal static bool IsPair(int type) => type > PairBase && type < PairBase + 5 * 16;
        internal static bool IsSimd(int type) => IsVector(type) || IsPair(type);
        internal static int Pair(int vector) => PairBase + vector - TagBase;
        internal static int PairItem(int pair) => TagBase + pair - PairBase;
        internal static int Chunks(int tag) => (tag - TagBase) / 16;
        internal static int Lane(int tag) => (tag - TagBase) % 16;
        internal static bool Wide(int tag) => Lane(tag) >= 0x0a;
        internal static bool Signed(int tag) => (Lane(tag) & 1) == 0;
        internal static int LaneType(int tag) => Wide(tag) ? I64 : I32;
        internal static int Bytes(int tag) => Chunks(tag) * 16;

        private static bool SupportedLane(int lane) => lane >= 0x08 && lane <= 0x0b;

        // The width (1, 2, 4) of System.Runtime.Intrinsics.Vector128`1/256`1/512`1 at a
        // TypeDefOrRef coded index; `generic` false for the static classes Vector128/256/512.
        // The types live in System.Runtime.Intrinsics (or a platform facade such as System.Runtime).
        internal static int ClassWidth(byte[] image, int[] layout, int[] heaps, int coded, bool generic)
        {
            if ((coded & 3) != 1) return 0; // TypeRef
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadType(image, heaps, layout, coded >> 2, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System.Runtime.Intrinsics")) return 0;
            int assembly = reference[AssemblyReferences.AssemblyRow];
            if (!PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts, assembly) &&
                !PlatformAssemblyIdentity.IsFrameworkAssembly(image, heaps, layout, counts, assembly, "System.Runtime.Intrinsics"))
                return 0;
            uint name = (uint)reference[2];
            if (MatchesAscii(image, heaps, name, generic ? "Vector128`1" : "Vector128")) return 1;
            if (MatchesAscii(image, heaps, name, generic ? "Vector256`1" : "Vector256")) return 2;
            if (MatchesAscii(image, heaps, name, generic ? "Vector512`1" : "Vector512")) return 4;
            return 0;
        }

        // A vector or pair tag for a resolved generic instantiation, or 0.
        internal static int Instantiation(byte[] image, int[] layout, int[] heaps, int coded, int[] arguments, int count)
        {
            if (count == 1)
            {
                int width = ClassWidth(image, layout, heaps, coded, true);
                if (width == 0 || !GenericScalar.IsScalar(arguments[0])) return 0;
                int lane = GenericScalar.Element(arguments[0]);
                return SupportedLane(lane) ? Tag(width, lane) : 0;
            }
            if (count == 2 && IsVector(arguments[0]) && arguments[0] == arguments[1] &&
                SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "ValueTuple`2"))
                return Pair(arguments[0]);
            return 0;
        }

        // ---- identification (before classification) ------------------------------------------

        // Rewrites the platform vector members in decoded records into SimdOp records
        // (operand = member, aux = the tag that fixes width and lane).
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records)
        {
            int[] counts = null;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (kind != MethodSpecCall && kind != MemberCall && kind != GetField &&
                    kind != LoadObject && kind != StoreObject) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                }
                var result = new int[2];
                int found = kind == MethodSpecCall ? GenericMember(image, layout, heaps, counts, records[slot + 1], records[slot + 3], result)
                    : kind == MemberCall ? Member(image, layout, heaps, records[slot + 1], records[slot + 3], result)
                    : kind == GetField ? TupleField(image, layout, heaps, records[slot + 1], records[slot + 3], result)
                    : IndirectVector(image, layout, heaps, records[slot + 1], kind, result);
                if (found == 0) continue;
                records[slot] = SimdOp;
                records[slot + 1] = result[0];
                records[slot + 3] = result[1];
            }
        }

        // ldobj/stobj of a vector TypeSpec token: result = [LoadIndirect/StoreIndirect, tag].
        private static int IndirectVector(byte[] image, int[] layout, int[] heaps, int token, int kind, int[] result)
        {
            if ((uint)token >> 24 != 0x1b) return 0;
            int tag = TypeSpecTag(image, layout, heaps, token & 0xffffff);
            if (!IsVector(tag)) return 0;
            result[0] = kind == LoadObject ? LoadIndirect : StoreIndirect;
            result[1] = tag;
            return 1;
        }

        // The tag of a TypeSpec `GENERICINST VALUETYPE VectorN`1 <lane>` (or a ValueTuple pair), or 0.
        internal static int TypeSpecTag(byte[] image, int[] layout, int[] heaps, int row)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob <= 0) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (size < 4 || size > limit - next[0]) return 0;
            int at = next[0];
            int tag = ReadTag(image, layout, heaps, at, at + size, next);
            return tag != 0 && next[0] == at + size ? tag : 0;
        }

        internal static int ReadSignatureTag(byte[] image, int[] layout, int[] heaps, int at, int end, int[] next) =>
            ReadTag(image, layout, heaps, at, end, next);

        private static int ReadTag(byte[] image, int[] layout, int[] heaps, int at, int end, int[] next)
        {
            if (at + 3 > end || image[at] != 0x15 || image[at + 1] != 0x11) return 0;
            int coded = CompressedUnsigned(image, at + 2, end, next);
            if (coded < 0 || next[0] >= end) return 0;
            int count = image[next[0]++];
            if (count == 1)
            {
                int width = ClassWidth(image, layout, heaps, coded, true);
                if (width == 0 || next[0] >= end) return 0;
                int lane = image[next[0]++];
                return SupportedLane(lane) ? Tag(width, lane) : 0;
            }
            if (count != 2 || !SpanMetadata.IsPlatformType(image, layout, heaps, coded, "System", "ValueTuple`2")) return 0;
            int first = ReadTag(image, layout, heaps, next[0], end, next);
            int second = IsVector(first) ? ReadTag(image, layout, heaps, next[0], end, next) : 0;
            return IsVector(first) && first == second ? Pair(first) : 0;
        }

        // `ldfld !0/!1 ValueTuple`2<V, V>::Item1/Item2`.
        private static int TupleField(byte[] image, int[] layout, int[] heaps, int row, int linked, int[] result)
        {
            if (linked != 1) return 0;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member, allowClosedParent: true) < 0) return 0;
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, row);
            if (parent < 0 || (parent & 7) != 4) return 0;
            int tag = TypeSpecTag(image, layout, heaps, parent >> 3);
            if (!IsPair(tag)) return 0;
            uint name = (uint)member[4];
            int op = MatchesAscii(image, heaps, name, "Item1") ? Item1 : MatchesAscii(image, heaps, name, "Item2") ? Item2 : 0;
            if (op == 0) return 0;
            result[0] = op;
            result[1] = tag;
            return 1;
        }

        // Non-generic members: Vector512`1<T>::op_*/get_*, VectorN::Create(scalar...), Widen,
        // Shuffle, ShiftRightLogical, and the ValueTuple pair constructor.
        private static int Member(byte[] image, int[] layout, int[] heaps, int row, int opcode, int[] result)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member, allowClosedParent: true) < 0) return 0;
            uint name = (uint)member[4];
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, row);
            if (parent < 0) return 0;
            int blob = member[5];
            if (blob < 1 || blob >= heaps[1]) return 0;
            uint s = (uint)(heaps[0] + blob);
            int parameters = image[s + 2];
            if ((parent & 7) == 4)
            {
                int tag = TypeSpecTag(image, layout, heaps, parent >> 3);
                if (IsPair(tag))
                {
                    if (opcode != 0x73 || !MatchesAscii(image, heaps, name, ".ctor") || parameters != 2) return 0;
                    result[0] = NewPair;
                    result[1] = tag;
                    return 1;
                }
                if (!IsVector(tag) || opcode != 0x28 || image[s + 1] != 0x00 && image[s + 1] != 0x20) return 0;
                int op = 0;
                if (parameters == 0)
                    op = MatchesAscii(image, heaps, name, "get_Indices") ? Indices : MatchesAscii(image, heaps, name, "get_Zero") ? Zero :
                        MatchesAscii(image, heaps, name, "get_One") ? One : 0;
                else if (parameters == 2)
                    op = MatchesAscii(image, heaps, name, "op_BitwiseAnd") ? And : MatchesAscii(image, heaps, name, "op_BitwiseOr") ? Or :
                        MatchesAscii(image, heaps, name, "op_Addition") ? Add : MatchesAscii(image, heaps, name, "op_Subtraction") ? Subtract :
                        MatchesAscii(image, heaps, name, "op_Multiply") ? Multiply :
                        MatchesAscii(image, heaps, name, "op_LeftShift") ? ShiftLeft : 0;
                if (op == 0 || image[s + 1] != 0x00) return 0; // static operators and properties
                result[0] = op;
                result[1] = tag;
                return 1;
            }
            if ((parent & 7) != 1) return 0; // MemberRefParent tag 1: TypeRef
            int width = ClassWidth(image, layout, heaps, (parent >> 3) << 2 | 1, false);
            if (width == 0 || opcode != 0x28 || image[s + 1] != 0x00) return 0;
            // The return type decides the lane: GENERICINST VALUETYPE VectorN`1 <lane>.
            var next = new int[1];
            int end = (int)s + 1 + image[s];
            int returned = ReadTag(image, layout, heaps, (int)s + 3, end, next);
            if (MatchesAscii(image, heaps, name, "Create") && IsVector(returned) && Chunks(returned) == width)
            {
                int lanes = Bytes(returned) / (Wide(returned) ? 8 : 4);
                if (parameters == 1) { result[0] = Splat; result[1] = returned; return 1; }
                if (parameters == lanes) { result[0] = CreateLanes; result[1] = returned; return 1; }
                return 0;
            }
            if (MatchesAscii(image, heaps, name, "Widen") && IsPair(returned) && parameters == 1 &&
                Lane(PairItem(returned)) == 0x0a && width == 4)
            {
                result[0] = Widen;
                result[1] = Tag(4, 0x08);
                return 1;
            }
            if (MatchesAscii(image, heaps, name, "Shuffle") && IsVector(returned) && parameters == 2 && Signed(returned))
            {
                result[0] = Shuffle;
                result[1] = returned;
                return 1;
            }
            if (MatchesAscii(image, heaps, name, "ShiftRightLogical") && IsVector(returned) && parameters == 2)
            {
                result[0] = ShiftRightLogical;
                result[1] = returned;
                return 1;
            }
            return 0;
        }

        // Generic static members of Vector128/256/512 instantiated with one primitive lane.
        private static int GenericMember(byte[] image, int[] layout, int[] heaps, int[] counts, int row, int flags, int[] result)
        {
            if ((flags & MethodSpecCallvirt) != 0) return 0;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0 ||
                spec[MethodSpecifications.Arity] != 1 ||
                spec[MethodSpecifications.Arguments] + 1 != spec[MethodSpecifications.End]) return 0;
            int lane = image[spec[MethodSpecifications.Arguments]];
            if (!SupportedLane(lane)) return 0;
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) == 0) return 0;
            int memberRow = target >> 1;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, memberRow, member, allowClosedParent: true) < 0) return 0;
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, memberRow);
            if (parent < 0 || (parent & 7) != 1) return 0;
            int width = ClassWidth(image, layout, heaps, (parent >> 3) << 2 | 1, false);
            if (width == 0) return 0;
            int blob = member[5];
            if (blob < 1 || blob >= heaps[1]) return 0;
            uint s = (uint)(heaps[0] + blob);
            if (image[s + 1] != 0x10 || image[s + 2] != 1) return 0; // static, one generic parameter
            int parameters = image[s + 3];
            uint name = (uint)member[4];
            int tag = Tag(width, lane);
            int op =
                parameters == 1 && MatchesAscii(image, heaps, name, "Load") ? Load :
                parameters == 2 && MatchesAscii(image, heaps, name, "Store") ? Store :
                parameters == 3 && MatchesAscii(image, heaps, name, "ConditionalSelect") ? Select :
                parameters == 2 && MatchesAscii(image, heaps, name, "Equals") ? Equal :
                parameters == 2 && MatchesAscii(image, heaps, name, "GreaterThan") ? Greater :
                parameters == 2 && MatchesAscii(image, heaps, name, "GreaterThanOrEqual") ? GreaterOrEqual :
                parameters == 2 && MatchesAscii(image, heaps, name, "LessThan") ? Less :
                parameters == 2 && MatchesAscii(image, heaps, name, "LessThanOrEqual") ? LessOrEqual :
                parameters == 2 && MatchesAscii(image, heaps, name, "Max") ? Max :
                parameters == 2 && MatchesAscii(image, heaps, name, "Min") ? Min :
                parameters == 1 && MatchesAscii(image, heaps, name, "ExtractMostSignificantBits") ? MostSignificantBits :
                parameters == 1 && MatchesAscii(image, heaps, name, "Abs") ? Abs :
                parameters == 1 && MatchesAscii(image, heaps, name, "AsInt32") ? AsInt32 :
                parameters == 1 && MatchesAscii(image, heaps, name, "AsInt64") ? AsInt64 :
                parameters == 1 && MatchesAscii(image, heaps, name, "GetLower") ? LowerHalf :
                parameters == 1 && MatchesAscii(image, heaps, name, "GetUpper") ? UpperHalf :
                parameters == 1 && MatchesAscii(image, heaps, name, "ToScalar") ? ToScalar :
                parameters == 2 && MatchesAscii(image, heaps, name, "GetElement") ? GetElement : 0;
            if (op == 0) return 0;
            // 64-bit unsigned compares and min/max have no Wasm instruction; they stay diagnosed.
            if (Wide(tag) && !Signed(tag) && (op >= Greater && op <= Min)) return 0;
            if ((op == LowerHalf || op == UpperHalf) && width == 1) return 0;
            if (op == GetElement && width != 1) return 0;
            result[0] = op;
            result[1] = tag;
            return 1;
        }

        // ---- verification ----------------------------------------------------------------------

        internal static int Arity(int op, int tag)
        {
            if (op == Indices || op == Zero || op == One) return 0;
            if (op == CreateLanes) return Bytes(tag) / (Wide(tag) ? 8 : 4);
            if (op == Select) return 3;
            if (op == Load || op == Splat || op == MostSignificantBits || op == Abs || op == AsInt32 || op == AsInt64 ||
                op == Widen || op == LowerHalf || op == UpperHalf || op == ToScalar || op == Item1 || op == Item2 || op == LoadIndirect) return 1;
            return 2;
        }

        // The stack type of operand `index` (0 = deepest).
        internal static int Input(int op, int tag, int index)
        {
            if (op == Load || op == LoadIndirect || op == StoreIndirect && index == 0 || op == Store && index == 1) return Native;
            if (op == Splat || op == CreateLanes) return LaneType(tag);
            if ((op == ShiftLeft || op == ShiftRightLogical || op == GetElement) && index == 1) return I32;
            if (op == NewPair) return PairItem(tag);
            return tag;
        }

        // The result stack type, or Void.
        internal static int Result(int op, int tag)
        {
            if (op == Store || op == StoreIndirect) return Void;
            if (op == MostSignificantBits) return I64;
            if (op == ToScalar || op == GetElement) return LaneType(tag);
            if (op == AsInt32) return Tag(Chunks(tag), 0x08);
            if (op == AsInt64) return Tag(Chunks(tag), 0x0a);
            if (op == Widen) return Pair(Tag(4, 0x0a));
            if (op == LowerHalf || op == UpperHalf) return Tag(Chunks(tag) / 2, Lane(tag));
            if (op == Item1 || op == Item2) return PairItem(tag);
            if (op == NewPair) return tag;
            return tag;
        }

        // ---- emission --------------------------------------------------------------------------

        private static int Simd(byte[] code, int used, int instruction)
        {
            code[used++] = 0xfd;
            return WasmBinary.Unsigned(code, used, instruction);
        }

        private static int Chunk(byte[] code, int used, int vector, int chunk)
        {
            used = Local(code, used, 0x20, vector);
            used = Gc(code, used, 2, VectorType); // struct.get $vector
            code[used++] = (byte)chunk;
            return used;
        }

        private static int ZeroChunk(byte[] code, int used)
        {
            used = Simd(code, used, 0x0c); // v128.const
            for (int i = 0; i < 16; i++) code[used++] = 0;
            return used;
        }

        // [chunks 0..count-1 on the stack] -> [] with zero padding, stored into `target`.
        private static int Finish(byte[] code, int used, int count, int target)
        {
            for (int i = count; i < 4; i++) used = ZeroChunk(code, used);
            used = Gc(code, used, 0, VectorType); // struct.new
            return Local(code, used, 0x21, target);
        }

        // The tag value used as the $vector heap type marker in Gc (WasmBinary maps it).
        private const int VectorType = TagBase + 16 + 0x08;

        // Operands are spill locals in order (deepest first); `target` receives the result.
        // `scratch` holds three i32 locals and `wide` two i64 locals for the lane-scalar members.
        internal static int Emit(byte[] code, int used, int op, int tag, int[] operands, int target, int[] scratch, int[] wide)
        {
            int width = Chunks(tag);
            bool w64 = Wide(tag), signed = Signed(tag);
            switch (op)
            {
                case Load:
                case LoadIndirect:
                    for (int c = 0; c < width; c++)
                    {
                        used = Local(code, used, 0x20, operands[0]);
                        used = Simd(code, used, 0x00); // v128.load
                        code[used++] = 0;
                        used = WasmBinary.Unsigned(code, used, c * 16);
                    }
                    return Finish(code, used, width, target);
                case Store:
                case StoreIndirect:
                {
                    int vector = op == Store ? operands[0] : operands[1], address = op == Store ? operands[1] : operands[0];
                    for (int c = 0; c < width; c++)
                    {
                        used = Local(code, used, 0x20, address);
                        used = Chunk(code, used, vector, c);
                        used = Simd(code, used, 0x0b); // v128.store
                        code[used++] = 0;
                        used = WasmBinary.Unsigned(code, used, c * 16);
                    }
                    return used;
                }
                case Splat:
                    for (int c = 0; c < width; c++)
                    {
                        used = Local(code, used, 0x20, operands[0]);
                        used = Simd(code, used, w64 ? 0x12 : 0x11);
                    }
                    return Finish(code, used, width, target);
                case CreateLanes:
                {
                    int perChunk = w64 ? 2 : 4;
                    for (int c = 0; c < width; c++)
                    {
                        used = Local(code, used, 0x20, operands[c * perChunk]);
                        used = Simd(code, used, w64 ? 0x12 : 0x11);
                        for (int l = 1; l < perChunk; l++)
                        {
                            used = Local(code, used, 0x20, operands[c * perChunk + l]);
                            used = Simd(code, used, w64 ? 0x1e : 0x1c); // replace_lane
                            code[used++] = (byte)l;
                        }
                    }
                    return Finish(code, used, width, target);
                }
                case Indices:
                case Zero:
                case One:
                    for (int c = 0; c < width; c++)
                    {
                        used = Simd(code, used, 0x0c);
                        int lanes = w64 ? 2 : 4;
                        for (int l = 0; l < lanes; l++)
                        {
                            long value = op == Zero ? 0 : op == One ? 1 : c * lanes + l;
                            for (int b = 0; b < (w64 ? 8 : 4); b++) code[used++] = (byte)(value >> (8 * b));
                        }
                    }
                    return Finish(code, used, width, target);
                case Select:
                    for (int c = 0; c < width; c++)
                    {
                        used = Chunk(code, used, operands[1], c);
                        used = Chunk(code, used, operands[2], c);
                        used = Chunk(code, used, operands[0], c);
                        used = Simd(code, used, 0x52); // v128.bitselect
                    }
                    return Finish(code, used, width, target);
                case Max:
                case Min:
                    if (w64)
                    {
                        // min(a, b) = a < b ? a : b; max(a, b) = a > b ? a : b (bitselect by mask)
                        for (int c = 0; c < width; c++)
                        {
                            used = Chunk(code, used, operands[0], c);
                            used = Chunk(code, used, operands[1], c);
                            used = Chunk(code, used, operands[0], c);
                            used = Chunk(code, used, operands[1], c);
                            used = Simd(code, used, op == Min ? 0xd8 : 0xd9); // i64x2.lt_s / gt_s
                            used = Simd(code, used, 0x52);
                        }
                        return Finish(code, used, width, target);
                    }
                    return Binary(code, used, width, operands, target,
                        op == Min ? (signed ? 0xb6 : 0xb7) : (signed ? 0xb8 : 0xb9));
                case Equal: return Binary(code, used, width, operands, target, w64 ? 0xd6 : 0x37);
                case Less: return Binary(code, used, width, operands, target, w64 ? 0xd8 : signed ? 0x39 : 0x3a);
                case Greater: return Binary(code, used, width, operands, target, w64 ? 0xd9 : signed ? 0x3b : 0x3c);
                case LessOrEqual: return Binary(code, used, width, operands, target, w64 ? 0xda : signed ? 0x3d : 0x3e);
                case GreaterOrEqual: return Binary(code, used, width, operands, target, w64 ? 0xdb : signed ? 0x3f : 0x40);
                case And: return Binary(code, used, width, operands, target, 0x4e);
                case Or: return Binary(code, used, width, operands, target, 0x50);
                case Add: return Binary(code, used, width, operands, target, w64 ? 0xce : 0xae);
                case Subtract: return Binary(code, used, width, operands, target, w64 ? 0xd1 : 0xb1);
                case Multiply: return Binary(code, used, width, operands, target, w64 ? 0xd5 : 0xb5);
                case ShiftLeft:
                case ShiftRightLogical:
                    for (int c = 0; c < width; c++)
                    {
                        used = Chunk(code, used, operands[0], c);
                        used = Local(code, used, 0x20, operands[1]);
                        used = Simd(code, used, op == ShiftLeft ? (w64 ? 0xcb : 0xab) : (w64 ? 0xcd : 0xad));
                    }
                    return Finish(code, used, width, target);
                case Abs:
                    for (int c = 0; c < width; c++)
                    {
                        used = Chunk(code, used, operands[0], c);
                        if (signed) used = Simd(code, used, w64 ? 0xc0 : 0xa0);
                    }
                    return Finish(code, used, width, target);
                case AsInt32:
                case AsInt64:
                    used = Local(code, used, 0x20, operands[0]);
                    return Local(code, used, 0x21, target);
                case MostSignificantBits:
                {
                    // bit i is the sign of lane i; chunk c contributes its bitmask << (c * lanes per chunk)
                    int perChunk = w64 ? 2 : 4;
                    for (int c = 0; c < width; c++)
                    {
                        used = Chunk(code, used, operands[0], c);
                        used = Simd(code, used, w64 ? 0xc4 : 0xa4); // bitmask
                        if (c != 0)
                        {
                            used = Const(code, used, c * perChunk);
                            code[used++] = 0x74; // i32.shl
                            code[used++] = 0x72; // i32.or
                        }
                    }
                    code[used++] = 0xad; // i64.extend_i32_u
                    return Local(code, used, 0x21, target);
                }
                case Widen:
                {
                    // lower = lanes 0..7 (chunks 0, 1), upper = lanes 8..15 (chunks 2, 3)
                    for (int half = 0; half < 2; half++)
                    {
                        for (int c = 0; c < 2; c++)
                        {
                            used = Chunk(code, used, operands[0], half * 2 + c);
                            used = Simd(code, used, signed ? 0xc7 : 0xc9); // extend_low
                            used = Chunk(code, used, operands[0], half * 2 + c);
                            used = Simd(code, used, signed ? 0xc8 : 0xca); // extend_high
                        }
                        used = Gc(code, used, 0, VectorType);
                    }
                    used = Gc(code, used, 0, PairType);
                    return Local(code, used, 0x21, target);
                }
                case NewPair:
                    used = Local(code, used, 0x20, operands[0]);
                    used = Local(code, used, 0x20, operands[1]);
                    used = Gc(code, used, 0, PairType);
                    return Local(code, used, 0x21, target);
                case Item1:
                case Item2:
                    used = Local(code, used, 0x20, operands[0]);
                    used = Gc(code, used, 2, PairType);
                    code[used++] = (byte)(op == Item1 ? 0 : 1);
                    return Local(code, used, 0x21, target);
                case LowerHalf:
                case UpperHalf:
                {
                    int half = width / 2;
                    for (int c = 0; c < half; c++) used = Chunk(code, used, operands[0], (op == UpperHalf ? half : 0) + c);
                    return Finish(code, used, half, target);
                }
                case ToScalar:
                    used = Chunk(code, used, operands[0], 0);
                    used = Simd(code, used, w64 ? 0x1d : 0x1b); // extract_lane 0
                    code[used++] = 0;
                    return Local(code, used, 0x21, target);
                case GetElement:
                {
                    // (uint)index >= 4 -> ArgumentOutOfRangeException; then select the lane.
                    used = Local(code, used, 0x20, operands[1]);
                    used = Const(code, used, 4);
                    code[used++] = 0x4f; // i32.ge_u
                    used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
                    used = Const(code, used, 0);
                    used = Local(code, used, 0x21, target);
                    for (int l = 0; l < 4; l++)
                    {
                        used = Chunk(code, used, operands[0], 0);
                        used = Simd(code, used, 0x1b);
                        code[used++] = (byte)l;
                        used = Local(code, used, 0x20, target);
                        used = Local(code, used, 0x20, operands[1]);
                        used = Const(code, used, l);
                        code[used++] = 0x46; // i32.eq
                        code[used++] = 0x1b; // select
                        used = Local(code, used, 0x21, target);
                    }
                    return used;
                }
                case Shuffle:
                    return width == 1 && !w64 ? Shuffle128(code, used, operands, target)
                        : ShuffleLanes(code, used, width, w64, operands, target, wide);
            }
            return used;
        }

        private static int Binary(byte[] code, int used, int width, int[] operands, int target, int instruction)
        {
            for (int c = 0; c < width; c++)
            {
                used = Chunk(code, used, operands[0], c);
                used = Chunk(code, used, operands[1], c);
                used = Simd(code, used, instruction);
            }
            return Finish(code, used, width, target);
        }

        // Vector128<int>.Shuffle(v, indices): i8x16.swizzle with byte indices idx*4 + 0..3 for an
        // index in [0, 4) and 0xff (a zero byte) otherwise.
        private static int Shuffle128(byte[] code, int used, int[] operands, int target)
        {
            used = Chunk(code, used, operands[0], 0);
            used = Chunk(code, used, operands[1], 0);
            used = Const(code, used, 4);
            used = Simd(code, used, 0x11);
            used = Simd(code, used, 0x3a); // i32x4.lt_u: lanes in range
            used = Simd(code, used, 0x4d); // v128.not: out-of-range lanes are all ones
            used = Chunk(code, used, operands[1], 0);
            used = Const(code, used, 0x04040404);
            used = Simd(code, used, 0x11);
            used = Simd(code, used, 0xb5); // i32x4.mul: idx * 4 in every byte
            used = Const(code, used, 0x03020100);
            used = Simd(code, used, 0x11);
            used = Simd(code, used, 0xae); // i32x4.add: + 0, 1, 2, 3 per byte
            used = Simd(code, used, 0x50); // v128.or
            used = Simd(code, used, 0x0e); // i8x16.swizzle
            return Finish(code, used, 1, target);
        }

        // General shuffle: lane i = (uint)idx[i] < lanes ? v[idx[i]] : 0, by comparing with each lane.
        private static int ShuffleLanes(byte[] code, int used, int width, bool w64, int[] operands, int target, int[] wide)
        {
            int perChunk = w64 ? 2 : 4, lanes = width * perChunk;
            byte extract = w64 ? (byte)0x1d : (byte)0x1b, replace = w64 ? (byte)0x1e : (byte)0x1c;
            for (int c = 0; c < width; c++)
            {
                used = ZeroChunk(code, used);
                for (int l = 0; l < perChunk; l++)
                {
                    // wide[0] = idx[c * perChunk + l], wide[1] = result lane (64-bit math for both widths)
                    used = Chunk(code, used, operands[1], c);
                    used = Simd(code, used, extract);
                    code[used++] = (byte)l;
                    if (!w64) code[used++] = 0xad; // i64.extend_i32_u
                    used = Local(code, used, 0x21, wide[0]);
                    code[used++] = 0x42; code[used++] = 0; // i64.const 0
                    used = Local(code, used, 0x21, wide[1]);
                    for (int j = 0; j < lanes; j++)
                    {
                        used = Chunk(code, used, operands[0], j / perChunk);
                        used = Simd(code, used, extract);
                        code[used++] = (byte)(j % perChunk);
                        if (!w64) code[used++] = 0xad;
                        used = Local(code, used, 0x20, wide[1]);
                        used = Local(code, used, 0x20, wide[0]);
                        code[used++] = 0x42; used = WasmBinary.Signed(code, used, j); // i64.const j
                        code[used++] = 0x51; // i64.eq
                        code[used++] = 0x1b;
                        used = Local(code, used, 0x21, wide[1]);
                    }
                    used = Local(code, used, 0x20, wide[1]);
                    if (!w64) code[used++] = 0xa7; // i32.wrap_i64
                    used = Simd(code, used, replace);
                    code[used++] = (byte)l;
                }
            }
            return Finish(code, used, width, target);
        }

        // ---- module types ----------------------------------------------------------------------

        // The marker tag the type section maps to the pair struct.
        private const int PairType = PairBase + 16 * 4 + 0x0a;

        internal const int TypeBytes = 2 + 4 * 2 + 2 + 2 * 3; // $vector: 4 x v128; $vectorPair: 2 x (ref null $vector)

        internal static int WriteTypes(byte[] output, int cursor, int vectorHeap)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 4;
            for (int i = 0; i < 4; i++) { output[cursor++] = 0x7b; output[cursor++] = 0; } // immutable v128
            output[cursor++] = 0x5f;
            output[cursor++] = 2;
            for (int i = 0; i < 2; i++)
            {
                output[cursor++] = 0x63; // ref null
                output[cursor++] = (byte)vectorHeap;
                output[cursor++] = 0;
            }
            return cursor;
        }

        // [] -> [] : every vector local starts as the zero vector and every pair local as a pair of them.
        internal static int InitializeLocals(byte[] code, int used, int[] frame, int arguments, int locals, int[] inlineLocations)
        {
            bool any = false;
            for (int i = arguments; i < arguments + locals; i++)
                if (IsSimd(frame[i]) && inlineLocations[i] == 0) any = true;
            if (!any) return used;
            for (int i = arguments; i < arguments + locals; i++)
            {
                if (!IsSimd(frame[i]) || inlineLocations[i] != 0) continue;
                used = ZeroVector(code, used);
                if (IsPair(frame[i]))
                {
                    used = ZeroVector(code, used);
                    used = Gc(code, used, 0, PairType);
                }
                used = Local(code, used, 0x21, i);
            }
            return used;
        }

        private static int ZeroVector(byte[] code, int used)
        {
            for (int c = 0; c < 4; c++) used = ZeroChunk(code, used);
            return Gc(code, used, 0, VectorType);
        }

        internal static int InitializeBytes(int[] frame, int arguments, int locals) =>
            frame == null ? 0 : 160 * Count(frame, arguments, locals);

        private static int Count(int[] frame, int arguments, int locals)
        {
            int count = 0;
            for (int i = arguments; i < arguments + locals && i < frame.Length; i++) if (IsSimd(frame[i])) count++;
            return count;
        }
    }
}
