using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ReadOnlyAggregateCalls
    {
        // An init-only field can lend its real cell to an exact managed call.
        // The CLI signature proves the element type, and the body proves that
        // the final argument is consumed solely by ordinary field reads.
        internal static bool ReadsFinalArgument(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methodCount, int[] records, int field, int[] clauses, int storage, int[] objects)
        {
            if (ObjectLayouts.IsExplicitValue(objects, storage) || ObjectLayouts.IsByRefLikeValue(objects, storage))
                return false;
            int call = records[field + 2] * Width;
            if (call >= records.Length || records[call] != Call ||
                ValueOperands.Entry(records, call / Width, clauses)) return false;
            int row = records[call + 1];
            if (row < 1 || row > methodCount) return false;
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            bool instance = (flags & 0x10) == 0;
            // callvirt to a nonvirtual instance method still performs its
            // ordinary null check. A virtual slot is outside this proof.
            if ((flags & 0x2440) != 0 || (implementation & 0x1037) != 0 ||
                records[call + 3] != 0 && (!instance || records[call + 3] != 1) ||
                ObjectLayouts.IsConstructor(image, heaps, layout, row)) return false;

            var shape = new int[256];
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int arity = SignatureTypes.Read(bundle, image, heaps, signature, shape, 0,
                instance ? ObjectLayouts.ObjectReference : 0, layout, objects, owner, 0);
            if (arity <= (instance ? 1 : 0) || shape[arity] != ValueLocation(storage))
                return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 ||
                body[1] == 0 || body[1] > 256 || body[2] == 0 || body[5] != 0) return false;
            int locals = 0;
            if (body[3] != 0)
            {
                int blob = StandaloneSignatures.BlobIndex(image, heaps, layout, methodCount, body[3]);
                if (blob < 0) return false;
                locals = SignatureTypes.Read(bundle, image, heaps, blob, shape, 0, -1,
                    layout, objects, owner, 0);
                if (locals < 0) return false;
            }
            return ProvesReadOnly(image, body[0], body[1], arity, locals);
        }

        private static bool ProvesReadOnly(byte[] image, int start, int length, int arity, int locals)
        {
            var decoded = new int[length * Width];
            var noClauses = new int[0];
            if (Decode(image, start, length, arity, locals, decoded, noClauses, false) < 0) return false;
            int argument = arity - 1;
            for (int at = 0; at < decoded.Length; at += Width)
            {
                if (decoded[at + 1] != argument) continue;
                int kind = decoded[at];
                if (kind == Store || kind == FrameAddress) return false;
                if (kind != Load) continue;
                int next = decoded[at + 2] * Width;
                if (next >= decoded.Length || decoded[next] != GetField ||
                    ValueOperands.Entry(decoded, next / Width, noClauses)) return false;
            }
            return true;
        }
    }
}
