using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class EnumFlagTests
    {
        // With two proven boxes of the same enum, HasFlag only observes their bits.
        // Leave both value producers in place and bypass the unused allocations.
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] objects, int[] records, int[] clauses)
        {
            for (int valueBox = 0; valueBox < records.Length; valueBox += Width)
            {
                if (records[valueBox] != BoxValue) continue;
                int token = records[valueBox + 1];
                int table = token >> 24;
                if (table != 1 && table != 2) continue;
                int valueCast = records[valueBox + 2] * Width;
                int flag = SkipEnumCast(image, layout, heaps, records, clauses, valueCast);
                if (flag >= records.Length || (records[flag] != Load &&
                    records[flag] != Constant && records[flag] != WideConstant)) continue;
                int flagBox = records[flag + 2] * Width;
                bool widensConstant = records[flag] == Constant && flagBox < records.Length &&
                    records[flagBox] == ConvertInteger && (records[flagBox + 1] == 0x6a || records[flagBox + 1] == 0x6e);
                if (widensConstant)
                {
                    if (ValueOperands.Entry(records, flagBox / Width, clauses)) continue;
                    // Roslyn loads small long-enum constants as i32 followed by conv.i8 or conv.u8.
                    flagBox = records[flagBox + 2] * Width;
                }
                if (flagBox >= records.Length || records[flagBox] != BoxValue || records[flagBox + 1] != token) continue;
                int flagCast = records[flagBox + 2] * Width;
                int call = SkipEnumCast(image, layout, heaps, records, clauses, flagCast);
                if (call >= records.Length || records[call] != MemberCall ||
                    (records[call + 3] != 0x28 && records[call + 3] != 0x6f) ||
                    !IsHasFlag(image, layout, heaps, records[call + 1])) continue;
                if (ValueOperands.Entry(records, flag / Width, clauses) ||
                    ValueOperands.Entry(records, flagBox / Width, clauses) ||
                    ValueOperands.Entry(records, call / Width, clauses)) continue;

                int coded = (token & 0xffffff) << 2 | (table == 1 ? 1 : 0);
                int type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects,
                    valueType: true, enumIdentity: true);
                if (!ObjectLayouts.IsEnumKey(type)) continue;
                int storage = StackType(ObjectLayouts.SignatureStorage(objects, type));
                if (storage != I64 && (storage != I32 || widensConstant)) continue;
                records[valueBox] = Nop;
                records[flagBox] = Nop;
                if (valueCast != flag) records[valueCast] = Nop;
                if (flagCast != call) records[flagCast] = Nop;
                records[call] = EnumHasFlag;
                records[call + 1] = storage;
                records[call + 3] = 0;
            }
        }

        private static int SkipEnumCast(byte[] image, int[] layout, int[] heaps, int[] records,
            int[] clauses, int slot)
        {
            if (slot >= records.Length || records[slot] != CastClass ||
                ValueOperands.Entry(records, slot / Width, clauses) ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, records[slot + 1], "System", "Enum"))
                return slot;
            return records[slot + 2] * Width;
        }

        private static bool IsHasFlag(byte[] image, int[] layout, int[] heaps, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "HasFlag")) return false;
            int owner = member[AssemblyReferences.Leaf] << 2 | 1;
            if (!SpanMetadata.IsPlatformType(image, layout, heaps, owner, "System", "Enum")) return false;
            int blob = member[5], limit = heaps[0] + heaps[1];
            if (blob < 1 || blob >= heaps[1]) return false;
            var cursor = new int[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            if (length < 5 || length > limit - start || U32(image, (uint)start) != 0x12020120) return false;
            int end = start + length;
            int flag = CompressedUnsigned(image, start + 4, end, cursor);
            return flag > 0 && cursor[0] == end &&
                SpanMetadata.IsPlatformType(image, layout, heaps, flag, "System", "Enum");
        }
    }
}
