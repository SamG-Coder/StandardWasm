using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CoreStringCreation
    {
        // The exposed constructor is a CLR internal call. Bind only the
        // platform signature with a ReadOnlySpan<char> parameter.
        internal static bool IsReadOnlySpanConstructor(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            int declaring, signature;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;
                declaring = reference[AssemblyReferences.ParentType];
                signature = reference[5];
            }
            else
            {
                if (!ObjectLayouts.IsConstructor(image, heaps, layout, row) ||
                    MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 ||
                    MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x1000) return false;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1) return false;
                declaring = owner << 2;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            if (!PlatformTypeReferences.IsString(image, heaps, layout, declaring)) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, signature, 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void)) return false;
            int span = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
            return span > 0 && SpanMetadata.IsDefinition(image, layout, heaps, span) &&
                SignatureReader.TakeElement(image, cursor, end, SignatureReader.Character) && cursor[0] == end;
        }

        // Only the verified private construction sequence may expose a
        // writable span of a string's backing array. The original checks,
        // delegate call, and return remain in the managed body.
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int row, int[] contextArguments, int[] frame, int[] records, int[] objects, int[] clauses)
        {
            LowerFreshStringSpan(bundle, image, layout, heaps, owner, frame, records, objects, clauses);
            if (contextArguments == null || contextArguments.Length != 1 || clauses.Length != 0 ||
                records.Length != 59 * Width || frame[0] != I32 || frame[1] != Native ||
                frame[2] != CharacterSpanCallback || frame[3] != String ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Create")) return;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "String")) return;

            if (!Tail(records, clauses)) return;
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[32 * Width + 1],
                "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, 0x18 }, true) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[40 * Width + 1],
                    "System", "String", "GetRawStringData", new byte[] { 0x20, 0, 0x10, 3 }, false) ||
                !IsSpanConstructor(bundle, image, layout, heaps, owner, records[46 * Width + 1], objects) ||
                CharacterSpanCallbackMetadata.Member(bundle, image, layout, heaps, owner,
                    records[52 * Width + 1], 0, contextArguments, objects) != CharacterSpanCallbackMetadata.Invoke ||
                !IsArgumentThrow(image, layout, heaps, counts, first, records, 5, 10, "ThrowArgumentNullException") ||
                !IsArgumentThrow(image, layout, heaps, counts, first, records, 25, 30, "ThrowArgumentOutOfRangeException")) return;

            records[32 * Width] = AllocateString;
            records[40 * Width] = Nop;
            records[46 * Width] = StringConstructionSpan;
            records[5 * Width] = ThrowFrameworkArgument;
            records[5 * Width + 1] = ExceptionTagsWasm.ArgumentNullTag;
            records[25 * Width] = ThrowFrameworkArgument;
            records[25 * Width + 1] = ExceptionTagsWasm.ArgumentOutOfRangeTag;
        }

        // CoreLib constructs a writable view immediately after allocating its
        // owner. Do not expose String's first field as a general writable byref.
        private static void LowerFreshStringSpan(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] frame, int[] records, int[] objects, int[] clauses)
        {
            for (int allocation = 0; allocation < records.Length; allocation += Width)
            {
                if (records[allocation] != Call || records[allocation + 3] != 0) continue;
                int store = Next(records, allocation, Store, clauses);
                if (store < 0) continue;
                int local = records[store + 1];
                if (frame[local] != String) continue;
                int load = Next(records, store, Load, clauses);
                if (load < 0 || records[load + 1] != local) continue;
                int field = Next(records, load, FieldAddress, clauses);
                if (field < 0 || records[field + 3] != 0) continue;
                int lengthOwner = Next(records, field, Load, clauses);
                if (lengthOwner < 0 || records[lengthOwner + 1] != local) continue;
                int length = Next(records, lengthOwner, Call, clauses);
                if (length < 0 || records[length + 3] != 1) continue;
                int span = Next(records, length, MemberCall, clauses);
                if (span < 0 || records[span + 3] != 0x73) continue;

                var counts = new int[45];
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                    !CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[allocation + 1],
                        "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, 0x18 }, true) ||
                    !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, records[field + 1]) ||
                    StringMetadata.AccessorDefinition(image, heaps, layout, records[length + 1]) != StringMetadata.Length ||
                    !IsSpanConstructor(bundle, image, layout, heaps, owner, records[span + 1], objects)) continue;

                records[allocation] = AllocateString;
                records[field] = Nop;
                records[span] = StringConstructionSpan;
            }
        }

        private static int Next(int[] records, int slot, int kind, int[] clauses)
        {
            int pc = records[slot + 2], next = pc * Width;
            return next > slot && next < records.Length && records[next] == kind &&
                !ValueOperands.Entry(records, pc, clauses) ? next : -1;
        }

        private static bool IsArgumentThrow(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int[] records, int pc, int nextPc, string name)
        {
            if (!At(records, pc, Call, -1, nextPc) || records[pc * Width + 3] != 0) return false;
            int row = records[pc * Width + 1];
            if (row < 1 || row > counts[6] ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "ThrowHelper"))
                return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 5 || size > limit - start || U32(image, (uint)start) != 0x11010100) return false;
            int type = CompressedUnsigned(image, start + 4, start + size, next);
            return next[0] == start + size && type > 0 && (type & 3) == 0 &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, type >> 2, "System", "ExceptionArgument");
        }

        private static bool Tail(int[] records, int[] clauses)
        {
            // The fresh string is used only to construct the callback span
            // and as the result after the callback returns.
            return At(records, 30, Load, 0, 31) &&
                At(records, 31, ConvertInteger, 0xd3, 32) &&
                At(records, 32, Call, -1, 37) && records[32 * Width + 3] == 0 &&
                At(records, 37, Store, 3, 38) &&
                At(records, 38, Load, 2, 39) &&
                At(records, 39, Load, 3, 40) &&
                At(records, 40, Call, -1, 45) && records[40 * Width + 3] == 1 &&
                At(records, 45, Load, 0, 46) &&
                At(records, 46, MemberCall, -1, 51) && records[46 * Width + 3] == 0x73 &&
                At(records, 51, Load, 1, 52) &&
                At(records, 52, MemberCall, -1, 57) && records[52 * Width + 3] == 0x6f &&
                At(records, 57, Load, 3, 58) &&
                At(records, 58, Return, -1, 59) &&
                NoInteriorEntry(records, clauses);
        }

        private static bool NoInteriorEntry(int[] records, int[] clauses)
        {
            for (int offset = 31; offset < 59; offset++)
                if (records[offset * Width] != 0 && ValueOperands.Entry(records, offset, clauses)) return false;
            return true;
        }

        private static bool At(int[] records, int offset, int kind, int operand, int next)
        {
            int slot = offset * Width;
            return records[slot] == kind && (operand < 0 || records[slot + 1] == operand) && records[slot + 2] == next;
        }

        private static bool IsSpanConstructor(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;
            int parent = reference[AssemblyReferences.GenericParent], blob = reference[5];
            if (parent == 0 || blob <= 0 || !Fits((uint)blob, 8, (uint)heaps[1])) return false;
            int at = heaps[0] + blob;
            // Instance void Span<T>..ctor(ref T, int).
            return U32(image, (uint)at) == 0x01022007 && U32(image, (uint)(at + 4)) == 0x08001310 &&
                ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                    parent, objects) == WritableCharacterSpan;
        }
    }
}
