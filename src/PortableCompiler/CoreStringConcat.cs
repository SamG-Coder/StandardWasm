using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CoreStringConcat
    {
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int method,
            int[] frame, int parameters, int locals, int[] records, int[] clauses)
        {
            if (parameters < 2 || parameters > 4 || locals != (parameters == 2 ? 1 : 0) ||
                clauses.Length != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "Concat"))
                return;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return;
            var signature = new byte[3 + parameters];
            signature[1] = (byte)parameters;
            for (int i = 2; i < signature.Length; i++) signature[i] = 0x0e;
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, method,
                "System", "String", "Concat", signature, true) ||
                parameters == 2 && frame[parameters] != I32) return;

            // The length and source of each copy must come from the same
            // immutable arguments seen by the original prefix checks.
            for (int at = 0; at < records.Length; at += Width)
                if ((records[at] == Store || records[at] == FrameAddress) && records[at + 1] < parameters) return;

            for (int start = 0; start < records.Length; start += Width)
            {
                if (!At(records, start, Load, 0)) continue;
                int allocation = SizingTail(image, layout, heaps, counts, first, records, clauses, start, parameters);
                if (allocation < 0) continue;
                var copies = new int[parameters];
                if (!CopyTail(image, layout, heaps, counts, first, records, clauses, allocation, copies)) continue;

                // The fresh result only reaches the exact copies and the
                // final return. No writable String location is introduced.
                records[allocation] = AllocateString;
                for (int i = 0; i < copies.Length; i++) records[copies[i]] = CopyConstructionString;
                return;
            }
        }

        private static int SizingTail(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int[] records, int[] clauses, int start, int parameters)
        {
            bool wide = parameters > 2;
            int at = start;
            for (int input = 0; input < parameters; input++)
            {
                at = ReadLength(image, layout, heaps, records, at, input);
                if (wide) at = Take(records, at, ConvertInteger, 0x6a);
                if (input != 0) at = Take(records, at, Binary, 0x6a);
                else if (!wide)
                {
                    at = Take(records, at, Store, parameters);
                    at = Take(records, at, Load, parameters);
                }
            }
            at = Take(records, at, Dup, -1);
            at = Take(records, at, Constant, wide ? int.MaxValue : 0);
            if (wide) at = Take(records, at, ConvertInteger, 0x6a);
            int guard = at;
            if (!At(records, guard, Conditional, -1) || records[guard + 3] != (wide ? 0x4c : 0x4e)) return -1;
            at = Next(records, guard);
            if (!StaticCall(records, at) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[at + 1],
                    "System", "ThrowHelper", "ThrowOutOfMemoryException_StringTooLong", new byte[] { 0, 0, 1 }, true))
                return -1;

            int continuation = Next(records, at);
            if (continuation < 0 || records[guard + 1] != continuation / Width ||
                !SingleBranch(records, continuation / Width, guard)) return -1;
            at = wide ? Take(records, continuation, ConvertInteger, 0x69) : continuation;
            bool native = At(records, at, ConvertInteger, 0xd3);
            if (native) at = Next(records, at);
            if (!StaticCall(records, at) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[at + 1],
                    "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, native ? (byte)0x18 : (byte)8 }, true))
                return -1;
            for (int pc = Next(records, start); pc <= at; pc = Next(records, pc))
                if (pc != continuation && ValueOperands.Entry(records, pc / Width, clauses)) return -1;
            return at;
        }

        private static bool SingleBranch(int[] records, int target, int guard)
        {
            for (int at = 0; at < records.Length; at += Width)
            {
                int kind = records[at];
                if (at != guard && (kind == Branch || kind == Conditional || kind == Leave) && records[at + 1] == target)
                    return false;
                if (kind == Switch)
                    for (int i = 0; i < records[at + 3]; i++)
                        if (SwitchTarget(records, records[at + 1], i) == target) return false;
            }
            return true;
        }

        private static bool CopyTail(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int[] records, int[] clauses, int allocation, int[] copies)
        {
            int at = Next(records, allocation), helper = 0;
            for (int input = 0; input < copies.Length; input++)
            {
                at = Take(records, at, Dup, -1);
                if (input == 0) at = Take(records, at, Constant, 0);
                else if (copies.Length == 2) at = Take(records, at, Load, copies.Length);
                else
                {
                    for (int preceding = 0; preceding < input; preceding++)
                    {
                        at = ReadLength(image, layout, heaps, records, at, preceding);
                        if (preceding != 0) at = Take(records, at, Binary, 0x6a);
                    }
                }
                at = Take(records, at, Load, input);
                if (!StaticCall(records, at)) return false;
                int target = records[at + 1];
                if (helper == 0)
                {
                    if (!IsCopyHelper(image, layout, heaps, counts, first, target)) return false;
                    helper = target;
                }
                if (target != helper) return false;
                copies[input] = at;
                at = Next(records, at);
            }
            if (!At(records, at, Return, -1) || Next(records, at) != records.Length) return false;
            for (int pc = Next(records, allocation); pc < records.Length; pc = Next(records, pc))
                if (ValueOperands.Entry(records, pc / Width, clauses)) return false;
            return true;
        }

        private static bool IsCopyHelper(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row)
        {
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, row,
                "System", "String", "CopyStringContent", new byte[] { 0, 3, 1, 0x0e, 8, 0x0e }, true) ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 7) != 1) return false;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 31 || body[3] != 0 || body[5] != 0)
                return false;
            uint at = (uint)body[0];
            // Load each _firstChar, offset only the destination, and move
            // exactly the source length in UTF-16 units.
            uint field = U32(image, at + 2), length = U32(image, at + 20);
            return U16(image, at) == 0x7c02 && U16(image, at + 6) == 0x2803 &&
                U16(image, at + 12) == 0x7c04 && U32(image, at + 14) == field &&
                field >> 24 == 4 && (field & 0xffffff) > 0 && (field & 0xffffff) <= (uint)counts[4] &&
                StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, (int)(field & 0xffffff)) &&
                U16(image, at + 18) == 0x6f04 && U16(image, at + 24) == 0x28e0 && image[at + 30] == 0x2a &&
                length >> 24 == 6 && (length & 0xffffff) > 0 && (length & 0xffffff) <= (uint)counts[6] &&
                StringMetadata.AccessorDefinition(image, heaps, layout, (int)(length & 0xffffff)) == StringMetadata.Length &&
                CoreStringRange.IsClosedMethod(image, layout, heaps, counts, first, U32(image, at + 8),
                    "System.Runtime.CompilerServices", "Unsafe", "Add", new byte[] { 3 },
                    new byte[] { 0x10, 1, 2, 0x10, 0x1e, 0, 0x10, 0x1e, 0, 8 }) &&
                CoreStringRange.IsCharacterMemmove(image, layout, heaps, counts, first, U32(image, at + 26));
        }

        private static int ReadLength(byte[] image, int[] layout, int[] heaps, int[] records, int at, int input)
        {
            int call = Take(records, at, Load, input);
            return At(records, call, Call, -1) && records[call + 3] == 1 &&
                StringMetadata.AccessorDefinition(image, heaps, layout, records[call + 1]) == StringMetadata.Length
                ? Next(records, call) : -1;
        }

        private static bool StaticCall(int[] records, int at) => At(records, at, Call, -1) && records[at + 3] == 0;

        private static int Take(int[] records, int at, int kind, int operand) =>
            At(records, at, kind, operand) ? Next(records, at) : -1;

        private static int Next(int[] records, int at) =>
            at >= 0 && at < records.Length ? records[at + 2] * Width : -1;

        private static bool At(int[] records, int at, int kind, int operand) =>
            at >= 0 && at < records.Length && records[at] == kind && (operand < 0 || records[at + 1] == operand);
    }
}
