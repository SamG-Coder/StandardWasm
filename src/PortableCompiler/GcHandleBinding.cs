using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class GcHandleBinding
    {
        internal const int Allocate = 1, Free = 2, Get = 3, Set = 4;

        internal static int Identify(byte[] image, int[] layout, int[] heaps, int row)
        {
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            int operation = MatchesAscii(image, heaps, name, "InternalAlloc") ? Allocate :
                MatchesAscii(image, heaps, name, "InternalFree") ? Free :
                MatchesAscii(image, heaps, name, "InternalGet") ? Get :
                MatchesAscii(image, heaps, name, "InternalSet") ? Set : 0;
            if (operation == 0 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) != 0x10)
                return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner,
                "System.Runtime.InteropServices", "GCHandle")) return 0;

            int flags = MethodDefinitions.ReadColumn(image, layout, row, 1);
            uint rva = (uint)MethodDefinitions.ReadColumn(image, layout, row, 0);
            if (operation == Set)
                return flags == 0x1000 && rva == 0 && HasSignature(image, layout, heaps, row,
                    new byte[] { 0, 2, 1, 0x18, 0x1c }) ? Set : 0;

            var body = new int[6];
            if (flags != 0 || MethodBodies.Read(image, rva, body) != 0 || body[5] != 0) return 0;
            uint start = (uint)body[0];
            if (operation == Get)
                return body[1] == 3 && body[2] >= 1 && body[3] == 0 &&
                    U16(image, start) == 0x5002 && image[start + 2] == 0x2a &&
                    HasSignature(image, layout, heaps, row, new byte[] { 0, 1, 0x1c, 0x18 }) ? Get : 0;
            if (operation == Free)
                return IsFree(image, layout, heaps, counts[6], row, owner, start, body) ? Free : 0;
            return IsAllocate(image, layout, heaps, counts, first, row, owner, start, body) ? Allocate : 0;
        }

        private static bool IsAllocate(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int row, int owner, uint start, int[] body)
        {
            if (!AllocateSignature(image, layout, heaps, counts, first, row) || body[1] != 21 || body[2] < 2 ||
                U16(image, start) != 0x0302 || image[start + 2] != 0x28 ||
                U32(image, start + 7) != 0x082d060a || U16(image, start + 11) != 0x0302 ||
                image[start + 13] != 0x28 || U16(image, start + 18) != 0x060a || image[start + 20] != 0x2a)
                return false;
            int locals = StandaloneSignatures.BlobIndex(image, heaps, layout, counts[6], body[3]);
            if (locals < 1 || !Fits((uint)locals, 4, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + locals)) != 0x18010703) return false;
            int fast = CallTarget(image, layout, heaps, counts[6], owner, U32(image, start + 3), "_InternalAlloc", 0x1000);
            int slow = CallTarget(image, layout, heaps, counts[6], owner, U32(image, start + 14), "InternalAllocWithGCTransition", 8);
            return fast != 0 && slow != 0 &&
                AllocateSignature(image, layout, heaps, counts, first, fast) &&
                AllocateSignature(image, layout, heaps, counts, first, slow);
        }

        private static bool IsFree(byte[] image, int[] layout, int[] heaps, int count,
            int row, int owner, uint start, int[] body)
        {
            var signature = new byte[] { 0, 1, 1, 0x18 };
            if (!HasSignature(image, layout, heaps, row, signature) || body[1] != 15 ||
                body[2] < 1 || body[3] != 0 || U16(image, start) != 0x2802 ||
                U32(image, start + 6) != 0x2802062d || image[start + 14] != 0x2a) return false;
            int fast = CallTarget(image, layout, heaps, count, owner, U32(image, start + 2), "_InternalFree", 0x1000);
            int slow = CallTarget(image, layout, heaps, count, owner, U32(image, start + 10), "InternalFreeWithGCTransition", 8);
            return fast != 0 && slow != 0 &&
                HasSignature(image, layout, heaps, fast, new byte[] { 0, 1, 2, 0x18 }) &&
                HasSignature(image, layout, heaps, slow, signature);
        }

        private static int CallTarget(byte[] image, int[] layout, int[] heaps, int count, int owner,
            uint token, string name, int implementation)
        {
            int row = (int)(token & 0xffffff);
            return token >> 24 == 6 && row > 0 && row <= count &&
                MethodDefinitions.ReadColumn(image, layout, row, 1) == implementation &&
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) == 0x10 &&
                MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name) &&
                ObjectLayouts.Owner(image, heaps, layout, row, false) == owner ? row : 0;
        }

        private static bool AllocateSignature(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 2, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x18) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x1c) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            return HandleType(image, layout, heaps, counts, first, cursor, end);
        }

        private static bool HandleType(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int[] cursor, int end)
        {
            int type = CompressedUnsigned(image, cursor[0], end, cursor);
            return type > 0 && (type & 3) == 0 && cursor[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, type >> 2,
                    "System.Runtime.InteropServices", "GCHandleType");
        }

        internal static int RestrictPinned(byte[] image, int[] layout, int[] heaps, int row, int[] body, int[] records, int[] clauses)
        {
            // CoreCLR performs pinnability checks only inside the original
            // pinned branches. Keep its normal and invalid-enum paths intact.
            if (body[5] != 0 || body[1] != 73 && body[1] != 56) return 0;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            bool constructor = body[1] == 73 && MatchesAscii(image, heaps, name, ".ctor");
            if (!constructor && (body[1] != 56 || !MatchesAscii(image, heaps, name, "set_Target"))) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) != 0) return 0;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner,
                "System.Runtime.InteropServices", "GCHandle")) return 0;
            uint start = (uint)body[0];
            int load = constructor ? 24 : 20, call = load + 1;
            if (U16(image, start + (uint)load) != 0x2803 || U16(image, start + (uint)call + 5) != 0x102d ||
                ValueOperands.Entry(records, load, clauses) || ValueOperands.Entry(records, call, clauses)) return 0;
            uint pinToken = U32(image, start + (uint)call + 1);
            int pin = (int)(pinToken & 0xffffff);
            if (pinToken >> 24 != 6 || pin < 1 || pin > counts[6] ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, pin, "System.Runtime.InteropServices",
                    "Marshal", "IsPinnable", new byte[] { 0, 1, 2, 0x1c }, true)) return 0;

            if (constructor)
            {
                // Both the initial enum range check and the branch which skips
                // pinnability decode the constructor's second argument.
                var cursor = new int[1];
                int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 2, cursor);
                if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 1) ||
                    !SignatureReader.TakeElement(image, cursor, end, 0x1c) ||
                    !SignatureReader.TakeElement(image, cursor, end, 0x11) ||
                    !HandleType(image, layout, heaps, counts, first, cursor, end) ||
                    U32(image, start) != 0x10361904 || U32(image, start + 20) != 0x18331904 ||
                    U16(image, start + 48) != 0x0403 || image[start + 50] != 0x28) return 0;
            }
            else
            {
                // The setter's pinned check consumes a copy of the native word.
                // Prove its low-bit test before limiting the fallthrough.
                if (!HasSignature(image, layout, heaps, row, new byte[] { 0x20, 1, 1, 0x1c }) ||
                    U16(image, start + 12) != 0x2825 || U16(image, start + 18) != 0x182c ||
                    U16(image, start + 49) != 0x2803) return 0;
                int pinned = CallTarget(image, layout, heaps, counts[6], owner, U32(image, start + 14), "IsPinned", 0x100);
                if (pinned == 0 || !HasSignature(image, layout, heaps, pinned, new byte[] { 0, 1, 2, 0x18 })) return 0;
                var proof = new int[6];
                uint rva = (uint)MethodDefinitions.ReadColumn(image, layout, pinned, 0);
                if (MethodBodies.Read(image, rva, proof) != 0 || proof[1] != 9 || proof[2] < 2 || proof[3] != 0 || proof[5] != 0)
                    return 0;
                uint check = (uint)proof[0];
                if (U32(image, check) != 0x5fd31702 || U32(image, check + 4) != 0x03fed316 || image[check + 8] != 0x2a)
                    return 0;
            }
            uint leafToken = U32(image, start + 51);
            int leaf = (int)(leafToken & 0xffffff);
            if (leafToken >> 24 != 6 || leaf < 1 || leaf > counts[6] ||
                Identify(image, layout, heaps, leaf) != (constructor ? Allocate : Set)) return 0;
            int failure = FindFailure(image, layout, heaps, "ThrowNotSupportedException");
            if (failure == 0) return -2;

            // The helper always throws. Keep an i32 predicate on its unreachable
            // fallthrough so the remaining original instruction layout is valid.
            int slot = load * BranchIl.Width;
            records[slot] = BranchIl.Call;
            records[slot + 1] = failure;
            records[slot + 3] = 0;
            slot = call * BranchIl.Width;
            records[slot] = BranchIl.Constant;
            records[slot + 1] = 0;
            records[slot + 3] = 0;
            return 0;
        }

        internal static int FindFailure(byte[] image, int[] layout, int[] heaps, string name)
        {
            int type = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "ThrowHelper");
            var range = new int[2];
            if (type <= 0 || ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
            var signature = new byte[] { 0, 0, 1 };
            for (int row = range[0]; row < range[1]; row++)
                if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) == 0x10 &&
                    MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name) &&
                    HasSignature(image, layout, heaps, row, signature)) return row;
            return 0;
        }

        private static bool HasSignature(byte[] image, int[] layout, int[] heaps, int row, byte[] signature)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob > heaps[1] - signature.Length - 1) return false;
            int start = heaps[0] + blob;
            if (image[start++] != signature.Length) return false;
            for (int i = 0; i < signature.Length; i++)
                if (image[start + i] != signature[i]) return false;
            return true;
        }
    }
}
