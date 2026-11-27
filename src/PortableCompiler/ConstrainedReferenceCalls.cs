using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ConstrainedReferenceCalls
    {
        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] objects, int[] methodArguments, int[] records, int address, int type,
            int[] clauses, int methodCount)
        {
            int next = records[address + 2] * Width, arguments = 0;
            while (next < records.Length)
            {
                // A constrained reference call dereferences the receiver after argument
                // evaluation. Moving the load earlier is valid only across instructions
                // that cannot modify the addressed slot or change control flow.
                if (ValueOperands.Entry(records, next / Width, clauses)) return 0;
                int kind = records[next];
                if (kind == ConstrainedInstance)
                {
                    int token = records[next + 1];
                    if (token >> 24 != 0x1b) return 0;
                    int exact = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                        owner, token & 0xffffff, context, objects, methodArguments);
                    if (exact < 0) return exact;
                    int method = records[next + 3];
                    if (exact != type || MemberArity(image, layout, heaps, method, methodCount) != arguments) return 0;
                    bool local = method >> 24 == 6;
                    records[address] = Load;
                    records[next] = local ? Call : MemberCall;
                    records[next + 1] = method & 0xffffff;
                    records[next + 3] = local ? 1 : 0x6f;
                    return 1;
                }
                if (kind == BoxValue)
                {
                    int token = records[next + 1];
                    if (arguments == 0 || token >> 24 != 0x1b) return 0;
                    int boxed = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                        owner, token & 0xffffff, context, objects, methodArguments);
                    if (!IsObjectReference(boxed) || ObjectLayouts.IsValueType(objects, boxed)) return 0;
                }
                else if (kind == Load || kind == NullLiteral || kind == Constant || kind == LoadString)
                    arguments++;
                else if (kind != Nop)
                    return 0;
                next = records[next + 2] * Width;
            }
            return 0;
        }

        private static int MemberArity(byte[] image, int[] layout, int[] heaps, int method, int methodCount)
        {
            int row = method & 0xffffff;
            int signature;
            if (method >> 24 == 6)
            {
                if (row < 1 || row > methodCount) return -1;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0)
                    return -1;
                signature = reference[5];
            }
            var cursor = new int[1];
            int end = GenericMethodSignatures.Start(image, heaps, signature, cursor, 0, instance: true);
            return end > 0 ? ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor) : -1;
        }
    }
}
