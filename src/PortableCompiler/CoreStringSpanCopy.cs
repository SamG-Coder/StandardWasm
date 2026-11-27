using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CoreStringSpanCopy
    {
        // Preserve the managed length check and its throw path. Replace only the
        // verified byte-layout inputs to Memmove with tracked character views.
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int row, int[] records, int[] objects, int[] clauses)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "String"))
                return;

            for (int check = 0; check < records.Length; check += Width)
            {
                if (!At(records, check, Load, 0)) continue;
                int measured = Next(records, check, Call, -1, clauses);
                int target = Next(records, measured, FrameAddress, 1, clauses);
                int targetLength = Next(records, target, MemberCall, -1, clauses);
                int guard = Next(records, targetLength, Conditional, -1, clauses);
                int start = Next(records, guard, FrameAddress, 1, clauses);
                if (start < 0 || records[guard + 3] != 0x4b || // bgt.un: source length > destination length
                    records[measured + 3] != 0 || records[targetLength + 3] != 0x28 ||
                    SpanMetadata.Accessor(bundle, image, layout, heaps, owner, records[targetLength + 1],
                        0, null, WritableCharacterSpan, objects) != SpanLength) continue;
                int destination = Next(records, start, GetField, -1, clauses);
                int source = Next(records, destination, Load, 0, clauses);
                int characters = Next(records, source, FieldAddress, -1, clauses);
                int sourceForLength = Next(records, characters, Load, 0, clauses);
                int length = Next(records, sourceForLength, Call, -1, clauses);
                int count = Next(records, length, ConvertInteger, 0xe0, clauses);
                int move = Next(records, count, MethodSpecCall, -1, clauses);
                if (move < 0 || records[guard + 1] <= move / Width || records[length + 1] != records[measured + 1] ||
                    records[destination + 3] != 1 || records[characters + 3] != 0 ||
                    records[length + 3] != 0 || records[move + 3] != 0 ||
                    !IsSpanReference(bundle, image, heaps, layout, owner, records[destination + 1], objects) ||
                    !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, records[characters + 1]) ||
                    StringMetadata.AccessorDefinition(image, heaps, layout, records[length + 1]) != StringMetadata.Length ||
                    !CoreStringRange.IsCharacterMemmove(image, layout, heaps, counts, first,
                        0x2b000000u | (uint)records[move + 1])) continue;

                // Only the move operands change. The guarded caller still
                // decides whether a short destination returns or throws.
                records[start] = Load;
                records[start + 1] = 0;
                records[destination] = StringAsSpan;
                records[destination + 1] = 0;
                records[destination + 3] = 0;
                records[source + 1] = 1;
                records[characters] = Nop;
                records[sourceForLength] = Nop;
                records[length] = Nop;
                records[count] = Nop;
                records[move] = CopyCharacterSpan;
                records[move + 3] = CharacterSpan;
            }
        }

        private static bool IsSpanReference(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int owner, int member, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                reference[AssemblyReferences.GenericParent] == 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "_reference") ||
                ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                    reference[AssemblyReferences.GenericParent], objects, 0, null) != WritableCharacterSpan) return false;

            // A field signature with one byref to the declaring generic parameter.
            int blob = reference[5];
            return blob > 0 && blob <= heaps[1] - 5 &&
                U32(image, (uint)(heaps[0] + blob)) == 0x13100604 &&
                image[heaps[0] + blob + 4] == 0;
        }

        private static bool At(int[] records, int slot, int kind, int operand) =>
            records[slot] == kind && (operand < 0 || records[slot + 1] == operand);

        private static int Next(int[] records, int slot, int kind, int operand, int[] clauses)
        {
            if (slot < 0) return -1;
            int pc = records[slot + 2], next = pc * Width;
            return next > slot && next < records.Length && At(records, next, kind, operand) &&
                !ValueOperands.Entry(records, pc, clauses) ? next : -1;
        }
    }
}
