using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CoreStringPointerConstructor
    {
        // Both framework profiles use this managed factory behind the
        // runtime-implemented allocator. The newobj operands are its inputs.
        internal static bool Matches(byte[] image, int[] layout, int[] heaps, int row, bool member)
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
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2017) != 6 ||
                    MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 ||
                    MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x1000) return false;
                declaring = ObjectLayouts.Owner(image, heaps, layout, row, false) << 2;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            return PlatformTypeReferences.IsString(image, heaps, layout, declaring) &&
                Signature(image, heaps, signature, true);
        }

        internal static int FindFactory(byte[] image, int[] layout, int[] heaps, int type)
        {
            var range = new int[2];
            int status = ObjectLayouts.MethodRange(image, heaps, layout, type, range);
            if (status < 0) return status;
            int constructor = 0, factory = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                if (Matches(image, layout, heaps, row, false))
                {
                    if (constructor != 0) return -5;
                    constructor = row;
                }
                if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2457) != 0x11 ||
                    MethodDefinitions.ReadColumn(image, layout, row, 1) != 0 ||
                    MethodDefinitions.ReadColumn(image, layout, row, 0) == 0 ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Ctor") ||
                    !Signature(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), false)) continue;
                if (factory != 0) return -5;
                factory = row;
            }
            return constructor == 0 || factory == 0 ? -2 : factory;
        }

        // Retain the original range checks and pointer computation. The
        // allocator/copy tail uses the existing native UTF-16 span snapshot.
        internal static void LowerCopy(byte[] image, int[] layout, int[] heaps, int row,
            int[] frame, int parameters, int locals, int[] records, int[] clauses)
        {
            if (parameters != 3 || locals != 2 || frame[0] != Native || frame[1] != I32 || frame[2] != I32 ||
                frame[3] != Native || frame[4] != Native || clauses.Length != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Ctor"))
                return;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System", "String", "Ctor",
                    new byte[] { 0, 3, 0x0e, 0x0f, 3, 8, 8 }, true)) return;

            for (int start = 0; start < records.Length; start += Width)
            {
                int second = Take(records, start, Load, 2);
                if (second < 0) continue;
                bool nativeLength = records[second] == ConvertInteger && records[second + 1] == 0xd3;
                int allocation = nativeLength ? Take(records, second, ConvertInteger, 0xd3) : second;
                int view = Take(records, allocation, Call, -1);
                if (view < 0 || records[allocation + 3] != 0 ||
                    !CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[allocation + 1],
                        "System", "String", "FastAllocateString",
                        new byte[] { 0, 1, 0x0e, nativeLength ? (byte)0x18 : (byte)8 }, true))
                    continue;
                int length = Take(records, view, Dup, -1);
                int at = Take(records, length, Call, -1);
                if (at < 0 || records[length + 3] != 1 ||
                    StringMetadata.AccessorDefinition(image, heaps, layout, records[length + 1]) != StringMetadata.Length)
                    continue;
                at = Take(records, at, ConvertInteger, 0xe0);
                at = Take(records, at, Store, 4);
                int field = Take(records, at, Dup, -1);
                at = Take(records, field, FieldAddress, -1);
                if (at < 0 || records[field + 3] != 0 ||
                    !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, records[field + 1]))
                    continue;
                at = Take(records, at, Load, 3);
                int copy = Take(records, at, Load, 4);
                int result = Take(records, copy, MethodSpecCall, -1);
                if (result < 0 || records[result] != Return || records[result + 2] != records.Length / Width ||
                    records[copy + 3] != 0 ||
                    !CoreStringRange.IsCharacterMemmove(image, layout, heaps, counts, first, 0x2b000000u | (uint)records[copy + 1]))
                    continue;

                int step = second;
                while (step < result && !ValueOperands.Entry(records, step / Width, clauses))
                    step = records[step + 2] * Width;
                if (step != result) continue;
                for (int slot = start; slot < result; slot += Width) records[slot] = 0;
                Record(records, start, Load, 3, second, 0);
                Record(records, second, Load, 2, view, 0);
                Record(records, view, NativeCharacterSpan, 0, copy, 0);
                Record(records, copy, StringFromSpan, 0, result, WritableCharacterSpan);
                return;
            }
        }

        private static int Take(int[] records, int slot, int kind, int operand)
        {
            if (slot < 0 || slot >= records.Length || records[slot] != kind ||
                operand >= 0 && records[slot + 1] != operand) return -1;
            int next = records[slot + 2] * Width;
            return next > slot && next < records.Length ? next : -1;
        }

        private static void Record(int[] records, int slot, int kind, int operand, int next, int extra)
        {
            records[slot] = kind;
            records[slot + 1] = operand;
            records[slot + 2] = next / Width;
            records[slot + 3] = extra;
        }

        private static bool Signature(byte[] image, int[] heaps, int blob, bool instance)
        {
            var cursor = new int[1];
            int end = instance ? SignatureReader.ReadInstance(image, heaps, blob, 3, cursor) :
                SignatureReader.ReadStatic(image, heaps, blob, 3, cursor);
            int at = cursor[0];
            return end - at == 5 && image[at] == (instance ? 1 : 0x0e) &&
                image[at + 1] == 0x0f && image[at + 2] == 3 && image[at + 3] == 8 && image[at + 4] == 8;
        }
    }
}
