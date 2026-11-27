using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class PrimitiveHashCalls
    {
        internal static bool LowerFrame(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int[] clauses, int address)
        {
            int call = records[address + 2] * Width;
            if (call >= records.Length || records[call] != ConstrainedInstance ||
                ValueOperands.Entry(records, call / Width, clauses) ||
                Identify(bundle, image, layout, heaps, owner, context, methodArguments, objects, records, call) == 0) return false;
            records[address] = Load;
            records[call] = Nop;
            return true;
        }

        internal static int Identify(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int call)
        {
            int token = records[call + 1], table = token >> 24, row = token & 0xffffff;
            int element;
            if (table == 0x1b)
            {
                int type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                    row, context, objects, methodArguments);
                if (!GenericScalar.IsScalar(type)) return 0;
                element = GenericScalar.Element(type);
            }
            else if (table == 1 || table == 2)
                element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, row << 2 | (table == 1 ? 1 : 0));
            else return 0;
            if (element != 8 && element != 9) return 0;

            var declaration = new int[2];
            if (ConstrainedValueCalls.ResolveObjectDeclaration(bundle, image, layout, heaps, owner, records[call + 3],
                context, methodArguments, objects, declaration) < 0) return 0;
            var methods = new int[4];
            var streams = new int[8];
            byte[] core = bundle.Get(declaration[0]);
            if (CompressedTables.FindWithHeaps(core, methods, streams) < 0 ||
                !MatchesHash(core, streams, methods, declaration[1])) return 0;
            int primitive = ArrayTypeRefs.CoreDefinition(core, streams, methods, element);
            if (primitive <= 0) return 0;
            var range = new int[2];
            if (ObjectLayouts.MethodRange(core, streams, methods, primitive, range) < 0) return 0;
            int hash = 0;
            for (int method = range[0]; method < range[1]; method++)
            {
                if (!MatchesHash(core, streams, methods, method)) continue;
                if (hash != 0 || (MethodDefinitions.ReadColumn(core, methods, method, 2) & 0x557) != 0x46) return 0;
                hash = method;
            }
            return hash != 0 && ReturnsReceiver(core, methods, hash, element) ? element : 0;
        }

        private static bool MatchesHash(byte[] image, int[] heaps, int[] layout, int row)
        {
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetHashCode") &&
                signature > 0 && Fits((uint)signature, 4, (uint)heaps[1]) &&
                U32(image, (uint)(heaps[0] + signature)) == 0x08002003; // instance int32 (); length prefix 3
        }

        private static bool ReturnsReceiver(byte[] image, int[] layout, int row, int element)
        {
            // The original body loads the receiver's signed or unsigned
            // 32-bit value and returns those bits unchanged.
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 3 || body[3] != 0 || body[5] != 0) return false;
            int start = body[0];
            return image[start] == 0x02 && image[start + 1] == (element == 8 ? 0x4a : 0x4b) && image[start + 2] == 0x2a;
        }
    }
}
