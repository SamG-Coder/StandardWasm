using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Resolve an exact MethodImpl declaration backed by a
    // local, final MethodDef. Other body and default-interface shapes diagnose.
    internal static class ExplicitInterfaceSlots
    {
        internal static int Find(BundleImages bundle, int[] objects, byte[] image, int[] layout,
            int[] heaps, int owner, int type, int declaredOwner, int declaredMethod,
            byte[] declaration, int[] declarationLayout, int[] declarationHeaps, int name, int signature,
            int declaredContext, int bodyContext)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[25] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 25);
            if (table < 0) return -1;
            int classWidth = counts[2] < 65536 ? 2 : 4;
            int methodWidth = MetadataTableRows.MethodRefWidth(counts);
            int width = classWidth + 2 * methodWidth;
            int selected = 0;
            var reference = new int[AssemblyReferences.SymbolWidth];
            var symbol = new int[5];
            for (int i = 0; i < counts[25]; i++)
            {
                uint at = (uint)(table + i * width);
                if (Index(image, at, classWidth) != type) continue;
                int coded = Index(image, at + (uint)(classWidth + methodWidth), methodWidth);
                int row = coded >> 1;
                if (row < 1 || row > counts[(coded & 1) == 0 ? 6 : 10]) return -1;
                if ((coded & 1) == 0)
                {
                    if (declaredContext != 0 || owner != declaredOwner || row != declaredMethod) continue;
                }
                else
                {
                    int status = AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true);
                    if (status < 0) return status;
                    if (!AssemblyReferences.SameString(image, heaps, reference[4], declaration, declarationHeaps, name))
                        continue;
                    status = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, row,
                        0x6f, false, objects, symbol, bodyContext, type, allowVirtual: true);
                    if (status < 0) return status;
                    if (symbol[0] != declaredOwner || symbol[1] != declaredMethod || symbol[3] != declaredContext) continue;
                }

                int body = Index(image, at + (uint)classWidth, methodWidth);
                if ((body & 1) != 0 || body >> 1 < 1 || body >> 1 > counts[6]) return -2;
                if (selected != 0) return -5;
                selected = body >> 1;
                if (ObjectLayouts.Owner(image, heaps, layout, selected, false) != type) return -2;
                int flags = MethodDefinitions.ReadColumn(image, layout, selected, 2);
                if ((flags & 0x70) != 0x60) return -2; // final instance virtual body
                int bodySignature = MethodDefinitions.ReadColumn(image, layout, selected, 4);
                int match = BundleSignatures.Match(bundle, declaration, declarationLayout, declarationHeaps,
                    declaredOwner, signature, image, layout, heaps, owner, bodySignature, false, objects, declaredContext, bodyContext);
                if (match <= 0) return match < 0 ? match : -2;
            }
            return selected;
        }

        private static int Index(byte[] image, uint at, int width) => width == 2 ? (int)U16(image, at) : (int)U32(image, at);
    }
}
