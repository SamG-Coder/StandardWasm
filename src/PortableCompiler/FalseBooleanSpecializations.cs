using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal sealed class FalseBooleanSpecializations
    {
        private int[] indices;

        internal int[] Indices(int size)
        {
            if (indices == null) indices = new int[size];
            return indices;
        }

        internal bool IsClone(int rowIndex, int function) =>
            indices != null && indices[rowIndex] == function + 1;

        internal static bool CanSpecialize(byte[] image, int[] layout, int[] heaps, int caller, int target,
            int flags, int[] records, int slot, int[] clauses)
        {
            // A direct private MethodDef has no replacement body or dynamic dispatch.
            if ((flags & 0x3c47) != 1 || slot < Width) return false;
            int value = slot - Width;
            if (records[value] != Constant || records[value + 1] != 0 || records[value + 2] != slot / Width ||
                ValueOperands.Entry(records, value / Width, clauses) ||
                ValueOperands.Entry(records, slot / Width, clauses)) return false;

            int declaring = ObjectLayouts.Owner(image, heaps, layout, target, false);
            if (declaring < 1 || declaring != ObjectLayouts.Owner(image, heaps, layout, caller, false) ||
                ObjectLayouts.IsConstructor(image, heaps, layout, target)) return false;

            int signature = MethodDefinitions.ReadColumn(image, layout, target, 4);
            bool instance = (flags & 0x10) == 0;
            if (!TakesBoolean(image, heaps, signature, instance)) return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, target, 0));
            return MethodBodies.Read(image, rva, body) == 0 && body[5] == 0 &&
                GuardLoad(image, body[0], body[1], instance) >= 0;
        }

        private static bool TakesBoolean(byte[] image, int[] heaps, int signature, bool instance)
        {
            var cursor = new int[1];
            int end = instance ? SignatureReader.ReadInstance(image, heaps, signature, 1, cursor)
                : SignatureReader.ReadStatic(image, heaps, signature, 1, cursor);
            if (end < 0) return false;
            int result = image[cursor[0]++];
            if (result == 0x12)
            {
                if (CompressedUnsigned(image, cursor[0], end, cursor) <= 0) return false;
            }
            else if ((result < 1 || result > 0x0e) && result != 0x1c) return false;
            return cursor[0] == end - 1 && image[cursor[0]] == SignatureReader.Boolean;
        }

        private static int GuardLoad(byte[] image, int start, int length, bool instance)
        {
            int pc = 0;
            // These instructions leave the argument untouched and the stack empty.
            while (pc < length)
            {
                if (image[start + pc] == 0) pc++;
                else if (length - pc > 1 && image[start + pc] == 0x14 &&
                    image[start + pc + 1] >= 0x0a && image[start + pc + 1] <= 0x0d) pc += 2;
                else break;
            }
            if (length - pc < 3 || image[start + pc] != (instance ? 3 : 2)) return -1;
            int branch = image[start + pc + 1];
            return branch == 0x2c || branch == 0x2d || branch == 0x39 || branch == 0x3a ? pc : -1;
        }

        internal static void Fold(byte[] image, int[] body, bool instance, int[] records, int[] clauses)
        {
            if (clauses.Length != 0) return;
            int pc = GuardLoad(image, body[0], body[1], instance);
            if (pc < 0) return;
            int load = pc * Width, branch = load + Width;
            if (records[load] != Load || records[load + 1] != (instance ? 1 : 0) ||
                records[load + 2] != pc + 1 || records[branch] != Conditional ||
                ValueOperands.Entry(records, pc, clauses) || ValueOperands.Entry(records, pc + 1, clauses)) return;

            int comparison = records[branch + 3];
            if (comparison != 0 && comparison != 0x45) return;
            records[load] = Nop;
            records[branch] = comparison == 0x45 ? Branch : Nop;
            records[branch + 3] = 0;
            RuntimeTypeBranches.RemoveUnreachable(records);
        }
    }
}
