using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class PinnedByteSpans
    {
        // Immutable PE backing has no writes to synchronize. Give a verified
        // read-only use a frame-owned copy of the original field in memory 0.
        // Array-backed, writable and escaping pin uses need a different lifetime.
        internal static void Lower(BundleImages bundle, byte[] image, int[] heaps, int[] layout, int owner, int context,
            int[] methodArguments, int[] objects, int[] records, int[] pinned, int[] frame, int parameters, int locals,
            int[] clauses, byte[] literals)
        {
            for (int i = 0; i < locals; i++)
            {
                if (pinned[i] != PinnedLocals.Byte) continue;
                int pin = parameters + i;
                for (int address = 0; address < records.Length; address += Width)
                {
                    if (records[address] != FrameAddress || frame[records[address + 1]] != ReadOnlyByteSpan) continue;
                    int call = Next(records, address, clauses);
                    if (!DirectMember(records, call)) continue;
                    int store = Next(records, call, clauses);
                    if (store < 0 || records[store] != Store || records[store + 1] != pin) continue;
                    int source = FindBacking(records, address, clauses);
                    if (source < 0 || !IsPinnableReference(bundle, image, heaps, layout, owner, context,
                        methodArguments, objects, records[call + 1])) continue;
                    int offset = records[source + 1];
                    int bytes = records[source + 3] == 0 ? 0 : records[source + Width + 1];
                    if (!CheckPointerUse(image, heaps, layout, records, pin, store, frame, parameters, clauses,
                        literals, offset, bytes)) continue;
                    records[address] = Nop;
                    records[call] = PinStaticBytes;
                    records[call + 1] = offset;
                    records[call + 3] = bytes;
                    break;
                }
            }
        }

        private static int FindBacking(int[] records, int address, int[] clauses)
        {
            int view = records[address + 1], store = -1;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot + 1] != view || slot == address) continue;
                if (records[slot] == FrameAddress) return -1;
                if (records[slot] != Store) continue;
                if (store >= 0 || slot >= address) return -1;
                store = slot;
            }
            if (store < 0 || Next(records, store, clauses) != address) return -1;
            for (int slot = 0; slot < store; slot += Width)
                if (records[slot] == LoadStaticByteSpan && Next(records, slot, clauses) == store) return slot;
            return -1;
        }

        private static bool CheckPointerUse(byte[] image, int[] heaps, int[] layout, int[] records, int pin,
            int store, int[] frame, int parameters, int[] clauses, byte[] literals, int start, int bytes)
        {
            int read = -1, clear = records.Length;
            int previous = -1, earlier = -1;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (records[slot + 1] == pin && slot != store)
                {
                    if (kind == FrameAddress) return false;
                    if (kind == Load)
                    {
                        if (slot <= store || read >= 0) return false;
                        read = slot;
                    }
                    if (kind == Store)
                    {
                        if (clear != records.Length || read < 0 || earlier < 0 || !ConvertToPointer(records, previous) ||
                            records[earlier] != Constant || records[earlier + 1] != 0 ||
                            Next(records, earlier, clauses) != previous || Next(records, previous, clauses) != slot)
                            return false;
                        clear = slot;
                    }
                }
                earlier = previous;
                previous = slot;
            }
            if (read < 0) return false;
            int convert = Next(records, read, clauses);
            if (!ConvertToPointer(records, convert)) return false;
            int use = Next(records, convert, clauses);
            if (use < 0) return false;
            if (records[use] != Store)
                return ReadOnlyUse(image, heaps, layout, records, use, clauses, literals, start, bytes);

            int pointer = records[use + 1];
            if (pointer < parameters || frame[pointer] != Native || pointer == pin) return false;
            int reads = 0;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (slot == use || records[slot + 1] != pointer) continue;
                int kind = records[slot];
                if (kind == Store || kind == FrameAddress) return false;
                if (kind != Load) continue;
                if (slot <= use || slot >= clear || !ReadOnlyUse(image, heaps, layout, records,
                    Next(records, slot, clauses), clauses, literals, start, bytes))
                    return false;
                reads++;
            }
            return reads != 0;
        }

        private static bool ReadOnlyUse(byte[] image, int[] heaps, int[] layout, int[] records, int use, int[] clauses,
            byte[] literals, int start, int bytes)
        {
            if (use < 0) return false;
            int offset = 0;
            if (records[use] == Constant)
            {
                offset = records[use + 1];
                int add = Next(records, use, clauses);
                if (add < 0 || records[add] != Binary || records[add + 1] != 0x6a) return false;
                use = Next(records, add, clauses);
            }
            if (use < 0 || (uint)offset >= (uint)bytes) return false;
            if (records[use] == LoadIndirect && records[use + 1] == 0x47) return true;

            // The native API reads a NUL-terminated name. Prove that the
            // original backing contains that terminator before lending it.
            bool terminated = false;
            for (int i = offset; i < bytes; i++)
                if (literals[start + i] == 0) { terminated = true; break; }
            if (!terminated) return false;
            int lookup = Next(records, use, clauses);
            if (!DirectMember(records, use) || !DirectMember(records, lookup)) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            var cursor = new int[1];
            if (!Win32Member(image, heaps, layout, records[use + 1], reference,
                "System.Private.Windows.Core", "Windows.Win32.Foundation", "PCSTR", "op_Explicit")) return false;
            int end = SignatureReader.ReadStatic(image, heaps, reference[5], 1, cursor);
            if (end < 0 || !ReadWin32Value(image, heaps, layout, cursor, end, "System.Private.Windows.Core", "PCSTR") ||
                end - cursor[0] != 2 || image[cursor[0]] != 0x0f || image[cursor[0] + 1] != 5) return false;
            if (!Win32Member(image, heaps, layout, records[lookup + 1], reference,
                "System.Windows.Forms.Primitives", "Windows.Win32", "PInvoke", "GetProcAddress")) return false;
            end = SignatureReader.ReadStatic(image, heaps, reference[5], 2, cursor);
            return end >= 0 && ReadWin32Value(image, heaps, layout, cursor, end, "System.Windows.Forms.Primitives", "FARPROC") &&
                ReadWin32Value(image, heaps, layout, cursor, end, "System.Private.Windows.Core", "HMODULE") &&
                ReadWin32Value(image, heaps, layout, cursor, end, "System.Private.Windows.Core", "PCSTR") && cursor[0] == end;
        }

        private static bool ReadWin32Value(byte[] image, int[] heaps, int[] layout, int[] cursor, int end,
            string assembly, string name)
        {
            if (cursor[0] >= end || image[cursor[0]++] != 0x11) return false;
            int token = CompressedUnsigned(image, cursor[0], end, cursor);
            if (token <= 0 || (token & 3) != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            return AssemblyReferences.ReadType(image, heaps, layout, token >> 2, reference) >= 0 &&
                Win32Type(image, heaps, reference, assembly, "Windows.Win32.Foundation", name);
        }

        private static bool Win32Member(byte[] image, int[] heaps, int[] layout, int row, int[] reference,
            string assembly, string space, string type, string name) =>
            AssemblyReferences.ReadMember(image, heaps, layout, row, reference) >= 0 &&
            MatchesAscii(image, heaps, (uint)reference[4], name) &&
            Win32Type(image, heaps, reference, assembly, space, type);

        private static bool Win32Type(byte[] image, int[] heaps, int[] reference, string assembly, string space, string type)
        {
            int key = reference[AssemblyReferences.KeyStart];
            return reference[AssemblyReferences.Depth] == 0 && reference[AssemblyReferences.KeyLength] == 8 &&
                reference[AssemblyReferences.FullKey] == 0 && U32(image, (uint)key) == 0x565c7ab7 &&
                U32(image, (uint)key + 4) == 0x89e03419 && MatchesAscii(image, heaps, (uint)reference[1], assembly) &&
                MatchesAscii(image, heaps, (uint)reference[2], type) && MatchesAscii(image, heaps, (uint)reference[3], space);
        }

        private static bool IsPinnableReference(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int owner, int context, int[] methodArguments, int[] objects, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "GetPinnableReference")) return false;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0 || ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                parent, objects, context, methodArguments) != ReadOnlyByteSpan) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], 0, cursor);
            if (end - cursor[0] < 4 || image[cursor[0]] != 0x1f) return false;
            int modifier = CompressedUnsigned(image, cursor[0] + 1, end, cursor);
            return modifier > 0 && SpanMetadata.IsPlatformType(image, layout, heaps, modifier,
                "System.Runtime.InteropServices", "InAttribute") && end - cursor[0] == 3 &&
                image[cursor[0]] == 0x10 && image[cursor[0] + 1] == 0x13 && image[cursor[0] + 2] == 0;
        }

        private static bool DirectMember(int[] records, int slot) =>
            slot >= 0 && records[slot] == MemberCall && records[slot + 3] == 0x28;

        private static bool ConvertToPointer(int[] records, int slot) =>
            slot >= 0 && records[slot] == ConvertInteger && records[slot + 1] == 0xe0;

        private static int Next(int[] records, int slot, int[] clauses)
        {
            int pc = records[slot + 2], next = pc * Width;
            return next > slot && next < records.Length && !ValueOperands.Entry(records, pc, clauses) ? next : -1;
        }
    }
}
