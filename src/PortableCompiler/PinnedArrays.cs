using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: `fixed (T* p = array)` over a primitive array.
    //
    // A Wasm GC array has no address, so the pinned address is a block of the module's NativeMemory
    // heap (NativeHeapWasm) holding a copy of the elements. Decision 0025 rejects copying in
    // general because the CLR makes managed and pointer accesses to a pinned array alias. This
    // lowering is applied only where that aliasing is proved impossible:
    //   - while any pin of the frame is live, neither this method nor any method it can call
    //     (transitively, read from raw IL) performs a managed element access, element address or
    //     length-independent array operation on an array of the same storage;
    //   - no call there can reach unknown code: virtual, interface, delegate and indirect calls,
    //     and platform members whose signature carries arrays, byrefs or spans, fail the proof;
    //   - the same array pinned twice in one frame shares one block.
    // The block is copied back into the array and freed when the pinned local is cleared, before
    // every ret, and when an exception leaves the frame (the frame cleanup region), so the array
    // holds every pointer write whenever managed code can observe it. Anything else stays a
    // located diagnostic.
    internal static class PinnedArrays
    {
        private const int MaxDepth = 24;

        // The Wasm vector of a pinnable primitive element type, or 0.
        internal static int Vector(int element) =>
            element == 0x04 || element == 0x05 ? Bytes : element == 0x06 || element == 0x07 || element == 0x03 ? Chars :
            element == 0x08 || element == 0x09 ? Ints : element == 0x0a || element == 0x0b ? Longs :
            element == 0x0c ? Singles : element == 0x0d ? Doubles : 0;

        internal static int ElementBytes(int vector) =>
            vector == Bytes ? 1 : vector == Chars ? 2 : vector == Longs || vector == Doubles ? 8 : 4;

        // Rewrites each proved pin of a pinned array local. Returns -2 (located at the pin) when a
        // pinned array local's pin cannot be proved alias-free, 0 otherwise.
        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] records, int[] pinned, int[] frame, int parameters, int locals, int[] clauses, int[] globals,
            int[] location)
        {
            bool any = false;
            for (int i = 0; i < locals; i++) any |= pinned[i] == PinnedLocals.Array;
            if (!any) return 0;
            int heap = -1;
            for (int i = 0; i < locals; i++)
            {
                if (pinned[i] != PinnedLocals.Array) continue;
                int local = parameters + i, vector = frame[local];
                for (int slot = 0; slot < records.Length; slot += Width)
                {
                    // ldloc L; ldc.i4.0; ldelema T; conv.u; stloc P
                    if (records[slot] != Load || records[slot + 1] != local) continue;
                    int zero = Next(records, slot, clauses), address = Next(records, zero, clauses);
                    int convert = Next(records, address, clauses), store = Next(records, convert, clauses);
                    if (store < 0 || records[zero] != Constant || records[zero + 1] != 0 ||
                        records[address] != ObjectElementAddress || records[convert] != ConvertInteger ||
                        records[convert + 1] != 0xe0 || records[store] != Store) continue;
                    int pointer = records[store + 1];
                    if (pointer < parameters || frame[pointer] != Native) continue;
                    if (!AliasFree(bundle, image, layout, heaps, owner, records, vector, 0, new int[64]))
                    {
                        if (location.Length != 0) location[2] = slot / Width;
                        return -2;
                    }
                    if (heap < 0)
                    {
                        heap = StaticFields.Register(globals, NativeMemoryMembers.HeapOwner, 0, I32);
                        if (heap < 0) return heap;
                    }
                    records[zero] = Nop;
                    records[address] = Nop;
                    records[convert] = PinArray;
                    records[convert + 1] = local;
                    records[convert + 3] = heap;
                }
                // ldnull; stloc L clears the pin: copy back first.
                for (int slot = 0; slot < records.Length; slot += Width)
                    if (records[slot] == Store && records[slot + 1] == local) records[slot + 3] = PinnedLocals.Array;
            }
            return 0;
        }

        // The pins of a frame: [pinned local, pointer local, vector, heap global] per PinArray record.
        internal static int[] Pins(int[] records, int[] frame)
        {
            int count = 0;
            for (int slot = 0; slot < records.Length; slot += Width) if (records[slot] == PinArray) count++;
            if (count == 0) return null;
            var pins = new int[count * 4];
            int at = 0;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != PinArray) continue;
                int local = records[slot + 1];
                pins[at] = local;
                pins[at + 1] = records[records[slot + 2] * Width + 1];
                pins[at + 2] = frame[local];
                pins[at + 3] = records[slot + 3];
                at += 4;
            }
            return pins;
        }

        private static int Next(int[] records, int slot, int[] clauses)
        {
            if (slot < 0) return -1;
            int next = records[slot + 2] * Width;
            return next < records.Length && !ValueOperands.Entry(records, next / Width, clauses) ? next : -1;
        }

        // Proves that no managed access to an array of `vector` storage can run in this method's
        // decoded records (other than its own pin sequences) or in anything it calls.
        private static bool AliasFree(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] records, int vector, int depth, int[] visited)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (kind == ObjectElementAddress)
                {
                    // Allowed only as the ldelema of a pin sequence: ldc.i4.0 before it, conv.u after it.
                    int next = records[slot + 2] * Width;
                    if (next >= records.Length || records[next] != ConvertInteger || records[next + 1] != 0xe0) return false;
                    continue;
                }
                if (kind == ArrayGet || kind == ArraySet)
                {
                    if (StorageOfOpcode(records[slot + 1]) == vector || StorageOfOpcode(records[slot + 1]) == 0) return false;
                    continue;
                }
                if (kind == InvokeDelegate || kind == CallIndirect) return false;
                if (kind == MemberCall)
                {
                    if (records[slot + 3] == 0x6f) return false;
                    if (!SafeMember(image, layout, heaps, records[slot + 1], vector)) return false;
                    continue;
                }
                if (kind == MethodSpecCall)
                {
                    if (records[slot + 3] != 0) return false;
                    if (!SafeMethodSpec(image, layout, heaps, records[slot + 1], vector)) return false;
                    continue;
                }
                if (kind == Call || kind == NewObject)
                {
                    if (kind == Call && records[slot + 3] != 0) return false; // callvirt
                    if (!SafeDefinition(bundle, image, layout, heaps, owner, records[slot + 1], vector, depth, visited)) return false;
                }
            }
            return true;
        }

        // The element storage an ldelem/stelem opcode accesses (0 = typed or reference: unknown).
        private static int StorageOfOpcode(int opcode) =>
            opcode == 0x90 || opcode == 0x91 || opcode == 0x9c ? Bytes :
            opcode == 0x92 || opcode == 0x93 || opcode == 0x9d ? Chars :
            opcode == 0x94 || opcode == 0x95 || opcode == 0x9e ? Ints :
            opcode == 0x96 || opcode == 0x9f ? Longs : opcode == 0x98 || opcode == 0xa0 ? Singles :
            opcode == 0x99 || opcode == 0xa1 ? Doubles : opcode == 0x97 || opcode == 0x9a || opcode == 0xa2 ? -1 : 0;

        // A MethodDef in this image: its raw IL must be alias-free too (recursively).
        private static bool SafeDefinition(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int row, int vector, int depth, int[] visited)
        {
            if (depth >= MaxDepth) return false;
            for (int i = 1; i <= visited[0]; i++) if (visited[i] == row) return true;
            if (visited[0] + 1 >= visited.Length) return false;
            visited[++visited[0]] = row;
            // A direct call runs exactly this body, whose IL is scanned, so class parameters are fine.
            // Its signature needs no check: every array it receives can only be reached through the
            // instructions and calls scanned below.
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (rva == 0) return false;
            if (MethodBodies.Read(image, rva, body) != 0) return false;
            return SafeIl(bundle, image, layout, heaps, owner, body[0], body[1], vector, depth + 1, visited);
        }

        // Scans raw IL opcodes. Unknown or unsafe instructions fail the proof.
        private static bool SafeIl(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int start, int length, int vector, int depth, int[] visited)
        {
            int pc = 0;
            while (pc < length)
            {
                int op = image[start + pc++];
                if (op == 0xfe)
                {
                    if (pc >= length) return false;
                    int extended = image[start + pc++];
                    // ldftn/ldvirtftn hand out code; calli is checked below; cpblk/initblk are pointer-only.
                    if (extended == 0x06 || extended == 0x07) return false;
                    pc += ExtendedOperand(extended);
                    continue;
                }
                if (op >= 0x8f && op <= 0xa4)
                {
                    // ldelema, ldelem.*, stelem.*, ldelem/stelem with tokens: a managed element access.
                    int storage = op == 0x8f || op == 0xa3 || op == 0xa4 ? 0 : StorageOfOpcode(op);
                    if (storage == 0 || storage == vector || storage == -1) return false;
                    if (op == 0x8f || op == 0xa3 || op == 0xa4) pc += 4;
                    continue;
                }
                if (op == 0x29 || op == 0x6f) return false; // calli, callvirt
                if (op == 0x28 || op == 0x73)
                {
                    if (pc + 4 > length) return false;
                    uint token = U32(image, (uint)(start + pc));
                    pc += 4;
                    int table = (int)(token >> 24), row = (int)(token & 0xffffff);
                    if (table == 0x06)
                    {
                        if (!SafeDefinition(bundle, image, layout, heaps, owner, row, vector, depth, visited)) return false;
                    }
                    else if (table == 0x0a)
                    {
                        if (!SafeMember(image, layout, heaps, row, vector)) return false;
                    }
                    else if (table == 0x2b)
                    {
                        if (!SafeMethodSpec(image, layout, heaps, row, vector)) return false;
                    }
                    else return false;
                    continue;
                }
                int operand = Operand(op);
                if (operand < 0)
                {
                    if (op != 0x45) return false;
                    if (pc + 4 > length) return false;
                    int targets = (int)U32(image, (uint)(start + pc));
                    pc += 4 + 4 * targets;
                    continue;
                }
                pc += operand;
            }
            return pc == length;
        }

        // Operand size of a one-byte opcode (ECMA-335 III); -1 for switch.
        private static int Operand(int op)
        {
            if (op == 0x45) return -1;
            if (op == 0x0e || op == 0x0f || op == 0x10 || op == 0x11 || op == 0x12 || op == 0x13 ||
                op == 0x1f || (op >= 0x2b && op <= 0x37) || op == 0xde) return 1;
            if (op == 0x20 || op == 0x22 || op == 0x27 || op == 0x28 || op == 0x29 || (op >= 0x38 && op <= 0x44) ||
                op == 0x6f || op == 0x70 || op == 0x71 || op == 0x72 || op == 0x73 || op == 0x74 || op == 0x75 ||
                op == 0x79 || (op >= 0x7b && op <= 0x81) || op == 0x8c || op == 0x8d || op == 0x8f ||
                op == 0xa3 || op == 0xa4 || op == 0xa5 || op == 0xc2 || op == 0xc6 || op == 0xd0 || op == 0xdd) return 4;
            if (op == 0x21 || op == 0x23) return 8;
            return 0;
        }

        private static int ExtendedOperand(int op) =>
            op == 0x09 || op == 0x0a || op == 0x0b || op == 0x0c || op == 0x0d || op == 0x0e ? 2 :
            op == 0x12 ? 1 : op == 0x06 || op == 0x07 || op == 0x15 || op == 0x16 || op == 0x1c || op == 0x1d ? 4 : 0;

        private static bool SafeMember(byte[] image, int[] layout, int[] heaps, int row, int vector)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0) return false;
            return SafeSignature(image, layout, heaps, reference[5]);
        }

        private static bool SafeMethodSpec(byte[] image, int[] layout, int[] heaps, int row, int vector)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return false;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0) return false;
            // Only primitive and SIMD vector instantiations: another generic argument could be an
            // array or span type.
            var cursor = new int[1];
            for (int at = spec[MethodSpecifications.Arguments]; at < spec[MethodSpecifications.End];)
            {
                if (image[at] >= 0x02 && image[at] <= 0x0d) { at++; continue; }
                cursor[0] = at;
                if (image[at] != 0x15 || !SafeType(image, layout, heaps, cursor, spec[MethodSpecifications.End])) return false;
                at = cursor[0];
            }
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) == 0) return false;
            return SafeMember(image, layout, heaps, target >> 1, vector);
        }

        // A platform method signature whose parameters and return carry no array, byref, object or
        // generic instance (such as Span<T>) cannot hand the callee a managed view of an array. The
        // one generic instance accepted is a SIMD vector of a primitive (Vector64/128/256/512<T>,
        // Vector<T>): a value of lanes that holds no reference.
        private static bool SafeSignature(byte[] image, int[] layout, int[] heaps, int blob)
        {
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (size < 1 || size > limit - next[0]) return false;
            int at = next[0], end = at + size;
            int convention = image[at++];
            if ((convention & 0x10) != 0)
            {
                CompressedUnsigned(image, at, end, next); // generic parameter count
                at = next[0];
            }
            int count = CompressedUnsigned(image, at, end, next);
            if (count < 0) return false;
            var cursor = new int[] { next[0] };
            for (int i = 0; i <= count; i++) // the return type, then each parameter
                if (!SafeType(image, layout, heaps, cursor, end)) return false;
            return cursor[0] == end;
        }

        private static bool SafeType(byte[] image, int[] layout, int[] heaps, int[] cursor, int end)
        {
            if (cursor[0] >= end) return false;
            int element = image[cursor[0]++];
            if (element >= 0x01 && element <= 0x0e || element == 0x18 || element == 0x19 || element == 0x1e && Skip(image, cursor, end))
                return true;
            if (element == 0x0f) return SafeType(image, layout, heaps, cursor, end); // pointer
            var next = new int[1];
            if (element == 0x11)
            {
                if (CompressedUnsigned(image, cursor[0], end, next) < 0) return false;
                cursor[0] = next[0];
                return true;
            }
            if (element != 0x15 || cursor[0] >= end || image[cursor[0]++] != 0x11) return false;
            int coded = CompressedUnsigned(image, cursor[0], end, next);
            if (coded < 0 || !IsSimdVector(image, layout, heaps, coded)) return false;
            if (CompressedUnsigned(image, next[0], end, next) != 1 || next[0] >= end) return false;
            cursor[0] = next[0];
            int lane = image[cursor[0]];
            if (lane == 0x1e) { cursor[0]++; return Skip(image, cursor, end); } // Vector512<!!0>
            if (lane == 0x13) { cursor[0]++; return Skip(image, cursor, end); } // Vector512<!0>
            if (lane < 0x02 || lane > 0x0d && lane != 0x18 && lane != 0x19) return false;
            cursor[0]++;
            return true;
        }

        // A TypeRef to System.Runtime.Intrinsics.Vector64/128/256/512`1 or System.Numerics.Vector`1.
        // A method instantiation of such a member is separately limited to primitive arguments.
        private static bool IsSimdVector(byte[] image, int[] layout, int[] heaps, int coded)
        {
            if ((coded & 3) != 1) return false;
            var type = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadType(image, heaps, layout, coded >> 2, type) < 0) return false;
            uint name = (uint)type[2], space = (uint)type[3];
            if (MatchesAscii(image, heaps, space, "System.Numerics")) return MatchesAscii(image, heaps, name, "Vector`1");
            return MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics") &&
                (MatchesAscii(image, heaps, name, "Vector64`1") || MatchesAscii(image, heaps, name, "Vector128`1") ||
                 MatchesAscii(image, heaps, name, "Vector256`1") || MatchesAscii(image, heaps, name, "Vector512`1"));
        }

        // Skips the compressed MVAR index after ELEMENT_TYPE_MVAR.
        private static bool Skip(byte[] image, int[] cursor, int end)
        {
            var next = new int[1];
            if (CompressedUnsigned(image, cursor[0], end, next) < 0) return false;
            cursor[0] = next[0];
            return true;
        }
    }
}
