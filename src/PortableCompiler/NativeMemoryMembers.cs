using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: System.Runtime.InteropServices.NativeMemory over memory 0 (decision 0060).
    // The host owns the heap: `standardwasm:native` alloc(size, alignment, zero) -> address,
    // free(address) and realloc(address, size, alignment) -> address manage blocks in pages it
    // grows itself, so they never overlap the shadow stack (decision 0065) or resource pages.
    // Clear, Fill and Copy are Wasm bulk-memory instructions.
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
            AlignedRealloc = 6, AlignedFree = 7, Free = 8, Realloc = 9, Clear = 10, Fill = 11, Copy = 12;
        internal const int ImportCount = 3, Capacity = 160;
        private const string Module = "standardwasm:native";
        // malloc alignment of the 32-bit CLR heap, rounded up to the Wasm SIMD width.
        private const int DefaultAlignment = 16;
        private const byte VoidPointer = 0x0f, NativeUnsigned = 0x19;

        // The member id of a NativeMemory MemberRef with exactly the .NET 10 signature, or 0.
        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 || !IsNativeMemoryType(image, heaps, layout,
                    reference[AssemblyReferences.ParentType])) return 0;
            var next = new int[1];
            int blob = reference[5], limit = heaps[0] + heaps[1];
            if (blob < 1 || blob >= heaps[1]) return 0;
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0];
            if (length < 3 || length > limit - at || image[at] != 0) return 0; // static, default
            int count = image[at + 1];
            if (count < 1 || count > 3 || length != 2 + Size(image, at + 2, at + length)) return 0;
            uint name = (uint)reference[4];
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
            member == AlignedRealloc || member == Fill || member == Copy ? 3 : 2;

        internal static bool Returns(int member) => member <= Realloc && member != AlignedFree && member != Free;

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
                else if (image[cursor++] != (kind == 'U' ? NativeUnsigned : 0x05)) return false;
            }
            return true;
        }

        internal static int TypeBytes => 7 + 4 + 7; // (i32 i32 i32)->i32, (i32)->(), (i32 i32 i32)->i32

        internal static int EntryBytes(int firstType) =>
            3 * (Module.Length + 3) + "alloc".Length + "free".Length + "realloc".Length +
            WasmBinary.UnsignedSize(firstType) + WasmBinary.UnsignedSize(firstType + 1) +
            WasmBinary.UnsignedSize(firstType + 2);

        internal static int WriteTypes(byte[] output, int cursor)
        {
            cursor = Triple(output, cursor);
            output[cursor++] = 0x60; output[cursor++] = 1; output[cursor++] = 0x7f; output[cursor++] = 0;
            return Triple(output, cursor);
        }

        private static int Triple(byte[] output, int cursor)
        {
            output[cursor++] = 0x60; output[cursor++] = 3;
            output[cursor++] = 0x7f; output[cursor++] = 0x7f; output[cursor++] = 0x7f;
            output[cursor++] = 1; output[cursor++] = 0x7f;
            return cursor;
        }

        internal static int WriteEntries(byte[] output, int cursor, int firstType)
        {
            cursor = Entry(output, cursor, "alloc", firstType);
            cursor = Entry(output, cursor, "free", firstType + 1);
            return Entry(output, cursor, "realloc", firstType + 2);
        }

        private static int Entry(byte[] output, int cursor, string name, int type)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Module);
            cursor = Win32ErrorWasm.WriteName(output, cursor, name);
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }

        // Operands are in `arguments` (in order); a returned address goes to `result`.
        // `first` is the alloc import; free and realloc follow it.
        internal static int Emit(byte[] code, int used, int member, int[] arguments, int result, int first)
        {
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
            {
                used = Get(code, used, arguments[0]);
                return Call(code, used, first + 1);
            }
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
            if (reallocate) used = Get(code, used, arguments[0]);
            int size = arguments[reallocate ? 1 : 0];
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
            else used = Get(code, used, size);
            if (aligned) used = Get(code, used, alignment);
            else used = BranchWasm.Const(code, used, DefaultAlignment);
            if (!reallocate) used = BranchWasm.Const(code, used, member == AllocZeroed || member == AllocZeroedCount ? 1 : 0);
            used = Call(code, used, reallocate ? first + 2 : first);
            used = BranchWasm.Local(code, used, 0x22, result);
            code[used++] = 0x45; // the host returns 0 only when it cannot allocate
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            return used;
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

        private static int Call(byte[] code, int used, int function)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, function);
        }
    }
}
