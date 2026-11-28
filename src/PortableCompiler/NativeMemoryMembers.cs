using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: System.Runtime.InteropServices.NativeMemory over memory 0 (decision 0060).
    // The module owns the heap (NativeHeapWasm): it lives in pages the module grows itself, so it
    // never overlaps the shadow stack (decision 0065) or host resource pages. Clear, Fill and Copy
    // are Wasm bulk-memory instructions.
    //
    // CLR-to-Wasm:
    // - A zero byte count allocates one byte, as .NET does, so a successful call never returns null.
    // - Allocation failure and an element-count product above nuint.MaxValue throw
    //   OutOfMemoryException on the CLR. Here they trap (`unreachable`).
    // - A non-power-of-two alignment throws the typed ArgumentException, as on the CLR.
    // - Clear/Fill/Copy of a nonzero length at address 0 trap, standing in for the CLR's
    //   access violation (decision 0060).
    internal static class NativeMemoryMembers
    {
        internal const int Alloc = 1, AllocCount = 2, AllocZeroed = 3, AllocZeroedCount = 4, AlignedAlloc = 5,
            AlignedRealloc = 6, AlignedFree = 7, Free = 8, Realloc = 9, Clear = 10, Fill = 11, Copy = 12,
            // Decision 0105: System.Buffer.MemoryCopy(void*, void*, (u)long destinationSizeInBytes, (u)long sourceBytesToCopy).
            BufferCopy = 13;
        internal const int Capacity = 1400;
        // The heap's control-block address lives in this module global (StaticFields registry).
        internal const int HeapOwner = int.MaxValue - 3;
        // malloc alignment of the 32-bit CLR heap, rounded up to the Wasm SIMD width.
        private const int DefaultAlignment = 16;
        private const byte VoidPointer = 0x0f, NativeUnsigned = 0x19;

        // The member id of a NativeMemory MemberRef with exactly the .NET 10 signature, or 0.
        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0) return 0;
            bool buffer = SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType], "System", "Buffer");
            if (!buffer && !IsNativeMemoryType(image, heaps, layout, reference[AssemblyReferences.ParentType])) return 0;
            var next = new int[1];
            int blob = reference[5], limit = heaps[0] + heaps[1];
            if (blob < 1 || blob >= heaps[1]) return 0;
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0];
            if (length < 3 || length > limit - at || image[at] != 0) return 0; // static, default
            int count = image[at + 1];
            if (count < 1 || count > 4 || length != 2 + Size(image, at + 2, at + length)) return 0;
            uint name = (uint)reference[4];
            if (buffer)
                return (Shape(image, at, "vPPLL") || Shape(image, at, "vPPII")) &&
                    MatchesAscii(image, heaps, name, "MemoryCopy") ? BufferCopy : 0;
            // Parameter kinds: P = void*, U = nuint, B = byte; the return is void or void*.
            if (Shape(image, at, "*U")) return MatchesAscii(image, heaps, name, "Alloc") ? Alloc :
                MatchesAscii(image, heaps, name, "AllocZeroed") ? AllocZeroed : 0;
            if (Shape(image, at, "*UU")) return MatchesAscii(image, heaps, name, "Alloc") ? AllocCount :
                MatchesAscii(image, heaps, name, "AllocZeroed") ? AllocZeroedCount :
                MatchesAscii(image, heaps, name, "AlignedAlloc") ? AlignedAlloc : 0;
            if (Shape(image, at, "*PUU")) return MatchesAscii(image, heaps, name, "AlignedRealloc") ? AlignedRealloc : 0;
            if (Shape(image, at, "*PU")) return MatchesAscii(image, heaps, name, "Realloc") ? Realloc : 0;
            if (Shape(image, at, "vP")) return MatchesAscii(image, heaps, name, "Free") ? Free :
                MatchesAscii(image, heaps, name, "AlignedFree") ? AlignedFree : 0;
            if (Shape(image, at, "vPU")) return MatchesAscii(image, heaps, name, "Clear") ? Clear : 0;
            if (Shape(image, at, "vPUB")) return MatchesAscii(image, heaps, name, "Fill") ? Fill : 0;
            if (Shape(image, at, "vPPU")) return MatchesAscii(image, heaps, name, "Copy") ? Copy : 0;
            return 0;
        }

        // NativeMemory is defined in System.Runtime.InteropServices (the reference assembly an
        // application compiles against) or reached through a platform assembly or CoreLib.
        private static bool IsNativeMemoryType(byte[] image, int[] heaps, int[] layout, int coded)
        {
            const string Space = "System.Runtime.InteropServices";
            if (SpanMetadata.IsPlatformType(image, layout, heaps, coded, Space, "NativeMemory")) return true;
            if ((coded & 3) != 1) return false;
            var counts = new int[45];
            var type = new int[AssemblyReferences.SymbolWidth];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                AssemblyReferences.ReadType(image, heaps, layout, coded >> 2, type) >= 0 &&
                type[AssemblyReferences.Depth] == 0 &&
                MatchesAscii(image, heaps, (uint)type[2], "NativeMemory") &&
                MatchesAscii(image, heaps, (uint)type[3], Space) &&
                PlatformAssemblyIdentity.IsFrameworkAssembly(image, heaps, layout, counts,
                    type[AssemblyReferences.AssemblyRow], Space);
        }

        internal static int Arity(int member) =>
            member == AlignedFree || member == Free || member == Alloc || member == AllocZeroed ? 1 :
            member == AlignedRealloc || member == Fill || member == Copy ? 3 : member == BufferCopy ? 4 : 2;

        internal static bool Returns(int member) => member <= Realloc && member != AlignedFree && member != Free;

        // The stack type of operand `index`: MemoryCopy's two sizes are 64-bit; the rest are wasm32 native ints.
        internal static bool WideOperand(int member, int index) => member == BufferCopy && index >= 2;

        // Encoded size of a return type and parameters made only of void, void*, nuint and byte.
        private static int Size(byte[] image, int at, int end)
        {
            int size = 0;
            while (at < end)
            {
                int step = image[at] == VoidPointer ? 2 : 1;
                size += step;
                at += step;
            }
            return size;
        }

        // `shape[0]` is the return ('v' void, '*' void*), then one letter per parameter.
        private static bool Shape(byte[] image, int at, string shape)
        {
            if (image[at + 1] != shape.Length - 1) return false;
            int cursor = at + 2;
            for (int i = 0; i < shape.Length; i++)
            {
                char kind = shape[i];
                if (kind == 'v') { if (image[cursor++] != 0x01) return false; }
                else if (kind == '*' || kind == 'P')
                {
                    if (image[cursor] != VoidPointer || image[cursor + 1] != 0x01) return false;
                    cursor += 2;
                }
                else if (image[cursor++] != (kind == 'U' ? NativeUnsigned : kind == 'L' ? 0x0b : kind == 'I' ? 0x0a : 0x05)) return false;
            }
            return true;
        }

        // Operands are in `arguments` (in order); a returned address goes to `result`. `heap` is the
        // control-block global and `scratch` holds NativeHeapWasm.ScratchCount i32 locals.
        internal static int Emit(byte[] code, int used, int member, int[] arguments, int result, int heap, int[] scratch)
        {
            if (member == BufferCopy)
            {
                // sourceBytesToCopy > destinationSizeInBytes -> ArgumentOutOfRangeException (unsigned, as
                // the CLR compares the (ulong) values; the long overload converts the same way).
                used = Get(code, used, arguments[3]);
                used = Get(code, used, arguments[2]);
                code[used++] = 0x56; // i64.gt_u
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
                // A count beyond the 32-bit address space cannot be a valid range: the CLR's access violation.
                used = Get(code, used, arguments[3]);
                code[used++] = 0x42; code[used++] = 32; // i64.const 32
                code[used++] = 0x88; // i64.shr_u
                code[used++] = 0xa7; // i32.wrap_i64
                code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b; // if: unreachable
                int count = scratch[NativeHeapWasm.Size];
                used = Get(code, used, arguments[3]);
                code[used++] = 0xa7;
                used = BranchWasm.Local(code, used, 0x21, count);
                used = TrapIfNullRange(code, used, arguments[0], count);
                used = TrapIfNullRange(code, used, arguments[1], count);
                // Buffer.Memmove semantics: memory.copy handles overlapping ranges.
                used = Get(code, used, arguments[1]);
                used = Get(code, used, arguments[0]);
                used = Get(code, used, count);
                code[used++] = 0xfc; code[used++] = 10; code[used++] = 0; code[used++] = 0; // memory.copy
                return used;
            }
            if (member == Clear || member == Fill || member == Copy)
            {
                // Clear(ptr, count), Fill(ptr, count, value), Copy(source, destination, count)
                int length = arguments[member == Copy ? 2 : 1];
                // A nonzero length at address 0 is the CLR's access violation.
                used = TrapIfNullRange(code, used, arguments[0], length);
                if (member == Copy) used = TrapIfNullRange(code, used, arguments[1], length);
                if (member == Copy)
                {
                    used = Get(code, used, arguments[1]); // destination
                    used = Get(code, used, arguments[0]); // source
                    used = Get(code, used, length);
                    code[used++] = 0xfc; code[used++] = 10; code[used++] = 0; code[used++] = 0; // memory.copy
                    return used;
                }
                used = Get(code, used, arguments[0]);
                if (member == Fill) used = Get(code, used, arguments[2]);
                else used = BranchWasm.Const(code, used, 0);
                used = Get(code, used, length);
                code[used++] = 0xfc; code[used++] = 11; code[used++] = 0; // memory.fill
                return used;
            }
            if (member == Free || member == AlignedFree)
                return NativeHeapWasm.Release(code, used, heap, arguments[0], scratch);
            bool aligned = member == AlignedAlloc || member == AlignedRealloc;
            int alignment = aligned ? arguments[member == AlignedAlloc ? 1 : 2] : -1;
            if (aligned)
            {
                // alignment == 0 || (alignment & (alignment - 1)) != 0 -> ArgumentException
                used = Get(code, used, alignment);
                code[used++] = 0x45; // i32.eqz
                used = Get(code, used, alignment);
                used = Get(code, used, alignment);
                used = BranchWasm.Const(code, used, 1);
                code[used++] = 0x6b; // i32.sub
                code[used++] = 0x71; // i32.and
                code[used++] = 0x72; // i32.or
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
            }
            bool reallocate = member == Realloc || member == AlignedRealloc;
            int size = scratch[NativeHeapWasm.Size];
            if (member == AllocCount || member == AllocZeroedCount)
            {
                // elementCount * elementSize must fit nuint (32 bits); otherwise OutOfMemoryException.
                used = Get(code, used, arguments[0]);
                code[used++] = 0xad; // i64.extend_i32_u
                used = Get(code, used, arguments[1]);
                code[used++] = 0xad;
                code[used++] = 0x7e; // i64.mul
                code[used++] = 0x42; code[used++] = 32; // i64.const 32
                code[used++] = 0x88; // i64.shr_u
                code[used++] = 0xa7; // i32.wrap_i64
                code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b; // if: unreachable
                used = Get(code, used, arguments[0]);
                used = Get(code, used, arguments[1]);
                code[used++] = 0x6c; // i32.mul
            }
            else used = Get(code, used, arguments[reallocate ? 1 : 0]);
            // .NET allocates one byte for a zero-byte request, so success never returns null.
            used = BranchWasm.Local(code, used, 0x22, size);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = BranchWasm.Const(code, used, 1);
            used = BranchWasm.Local(code, used, 0x21, size);
            code[used++] = 0x0b;
            // alignment = max(requested, 8), or the default malloc alignment
            int alignmentLocal = scratch[NativeHeapWasm.Alignment];
            if (aligned)
            {
                used = Get(code, used, alignment);
                used = BranchWasm.Const(code, used, 8);
                used = Get(code, used, alignment);
                used = BranchWasm.Const(code, used, 8);
                code[used++] = 0x4b; // i32.gt_u
                code[used++] = 0x1b; // select
            }
            else used = BranchWasm.Const(code, used, DefaultAlignment);
            used = BranchWasm.Local(code, used, 0x21, alignmentLocal);
            used = reallocate ? NativeHeapWasm.Resize(code, used, heap, arguments[0], scratch)
                : NativeHeapWasm.Allocate(code, used, heap, scratch, member == AllocZeroed || member == AllocZeroedCount);
            used = Get(code, used, scratch[NativeHeapWasm.Result]);
            return BranchWasm.Local(code, used, 0x21, result);
        }

        // Traps when address == 0 && length != 0.
        private static int TrapIfNullRange(byte[] code, int used, int address, int length)
        {
            used = Get(code, used, address);
            code[used++] = 0x45; // i32.eqz
            used = Get(code, used, length);
            used = BranchWasm.Const(code, used, 0);
            code[used++] = 0x47; // i32.ne
            code[used++] = 0x71; // i32.and
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b; // if: unreachable
            return used;
        }

        private static int Get(byte[] code, int used, int local) => BranchWasm.Local(code, used, 0x20, local);

    }
}
