using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class ExceptionFilters
    {
        // Recognize a pure two-type disjunction. Check every control-flow
        // target and keep the filter locals private to the removed block.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int start, int end, int[] objects, int[] filter, int slot)
        {
            var cursor = new int[] { start };
            int guard = TypeTest(image, cursor, end);
            if (guard < 0 || !Take(image, cursor, end, 0x25)) return -2;
            int acceptException = BranchTarget(image, cursor, end, 0x2d);
            if (!Take(image, cursor, end, 0x26) || !Take(image, cursor, end, 0x16)) return -2;
            int rejectException = BranchTarget(image, cursor, end, 0x2b);
            if (acceptException != cursor[0]) return -2;
            int exception = LocalIndex(image, cursor, end, true);
            if (exception < 0 || LocalIndex(image, cursor, end, false) != exception) return -2;
            int left = TypeTest(image, cursor, end);
            int acceptLeft = BranchTarget(image, cursor, end, 0x2d);
            if (left < 0 || LocalIndex(image, cursor, end, false) != exception) return -2;
            int right = TypeTest(image, cursor, end);
            int rejectRight = BranchTarget(image, cursor, end, 0x2c);
            if (right < 0 || acceptLeft != cursor[0] || !Take(image, cursor, end, 0x17)) return -2;
            int decision = LocalIndex(image, cursor, end, true);
            if (decision < 0 || decision == exception) return -2;
            int loadDecision = BranchTarget(image, cursor, end, 0x2b);
            if (rejectRight != cursor[0] || !Take(image, cursor, end, 0x16) ||
                LocalIndex(image, cursor, end, true) != decision || loadDecision != cursor[0] ||
                LocalIndex(image, cursor, end, false) != decision ||
                !Take(image, cursor, end, 0x16) || !Take(image, cursor, end, 0xfe) ||
                !Take(image, cursor, end, 3) || rejectException != cursor[0] ||
                !Take(image, cursor, end, 0xfe) || !Take(image, cursor, end, 0x11) || cursor[0] != end)
                return -2;

            if (!IsExceptionGuard(image, layout, heaps, guard)) return -2;
            int mask = ManagedExceptionClauses.ClauseMask(bundle, image, layout, heaps, owner, left, objects);
            int other = ManagedExceptionClauses.ClauseMask(bundle, image, layout, heaps, owner, right, objects);
            if (mask <= 0 || other <= 0) return -2;
            if (ManagedExceptionClauses.CatchesObjects(mask) || ManagedExceptionClauses.CatchesObjects(other))
            {
                int baseMask = ManagedExceptionClauses.ClauseMask(bundle, image, layout, heaps, owner, guard, objects);
                if (!ManagedExceptionClauses.CatchesObjects(baseMask)) return -2;
                int root = ManagedExceptionClauses.CaughtClass(baseMask);
                if (!IsExceptionSubtype(objects, mask, root) || !IsExceptionSubtype(objects, other, root)) return -2;
            }
            filter[slot] = other;
            filter[slot + 1] = exception + 1;
            filter[slot + 2] = decision + 1;
            return mask | ManagedExceptionClauses.FixedBits(other);
        }

        private static bool Take(byte[] image, int[] cursor, int end, int expected)
        {
            int at = cursor[0];
            if (at >= end || image[at] != expected) return false;
            cursor[0] = at + 1;
            return true;
        }

        private static int TypeTest(byte[] image, int[] cursor, int end)
        {
            if (!Take(image, cursor, end, 0x75) || end - cursor[0] < 4) return -1;
            int token = unchecked((int)U32(image, (uint)cursor[0]));
            cursor[0] = cursor[0] + 4;
            return token;
        }

        private static int BranchTarget(byte[] image, int[] cursor, int end, int opcode)
        {
            if (!Take(image, cursor, end, opcode) || cursor[0] >= end) return -1;
            int at = cursor[0];
            cursor[0] = at + 1;
            return cursor[0] + unchecked((sbyte)image[at]);
        }

        private static int LocalIndex(byte[] image, int[] cursor, int end, bool store)
        {
            int at = cursor[0];
            if (at >= end) return -1;
            int opcode = image[at], first = store ? 0x0a : 6;
            if (opcode >= first && opcode < first + 4)
            {
                cursor[0] = at + 1;
                return opcode - first;
            }
            if (opcode == (store ? 0x13 : 0x11) && end - at >= 2)
            {
                cursor[0] = at + 2;
                return image[at + 1];
            }
            if (opcode == 0xfe && end - at >= 4 && image[at + 1] == (store ? 0x0e : 0x0c))
            {
                cursor[0] = at + 4;
                return (int)U16(image, (uint)at + 2);
            }
            return -1;
        }

        internal const int Width = 3;

        private static bool IsExceptionSubtype(int[] objects, int mask, int root)
        {
            if (!ManagedExceptionClauses.CatchesObjects(mask)) return true;
            int type = ManagedExceptionClauses.CaughtClass(mask);
            return type == root || ObjectLayouts.Inherits(objects, type, root);
        }

        internal static bool IsExceptionGuard(byte[] image, int[] layout, int[] heaps, int token)
        {
            if ((uint)token >> 24 != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadType(image, heaps, layout, token & 0xffffff, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[2], "Exception") ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System")) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts, reference[AssemblyReferences.AssemblyRow]);
        }

        internal static int LocalKind(int[] filters, int local)
        {
            for (int slot = 0; slot < filters.Length; slot += Width)
            {
                if (filters[slot + 1] == local + 1) return 1;
                if (filters[slot + 2] == local + 1) return 2;
            }
            return 0;
        }

        internal static int ValidateLocals(int[] records, int[] filters, int parameters, int locals, int[] location)
        {
            for (int clause = 0; clause < filters.Length; clause += Width)
            {
                if (filters[clause] == 0) continue;
                int exception = filters[clause + 1] - 1;
                int decision = filters[clause + 2] - 1;
                if (exception >= locals || decision >= locals) return -1;
                for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                {
                    int kind = records[slot];
                    if (kind != BranchIl.Load && kind != BranchIl.Store && kind != BranchIl.FrameAddress) continue;
                    int local = records[slot + 1] - parameters;
                    if (local != exception && local != decision) continue;
                    if (location.Length != 0) location[2] = slot / BranchIl.Width;
                    return -2;
                }
            }
            return 0;
        }
    }
}
