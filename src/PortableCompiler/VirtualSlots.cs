using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class VirtualSlots
    {
        internal static int SelectVirtualBody(BundleImages bundle, int[] objects,
            int actual, bool allocated, int declared, int owner, int row, int[] symbol)
        {
            int receiver = actual == NullValue ? declared : actual;
            if (receiver < FirstClass || receiver - FirstClass >= objects[0] ||
                (!allocated && !IsSealed(objects, receiver)) ||
                IsValueType(objects, receiver) || IsInterface(objects, receiver)) return -2;

            var ancestors = new int[objects[0]];
            int count = 0;
            for (int type = receiver; type != declared; type = Parent(objects, type))
            {
                if (type < FirstClass || count >= ancestors.Length) return -2;
                ancestors[count++] = type;
            }

            byte[] source = bundle.Get(owner);
            var sourceLayout = new int[4];
            var sourceHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(source, sourceLayout, sourceHeaps) < 0) return -1;
            int name = MethodDefinitions.ReadColumn(source, sourceLayout, row, 3);
            int signature = MethodDefinitions.ReadColumn(source, sourceLayout, row, 4);
            int sourceType = TypeDef(objects, declared);
            int explicitSlot = VirtualSlotMetadata.HasNamedMethodImpl(source, sourceHeaps, sourceLayout, sourceType,
                source, sourceHeaps, name);
            if (explicitSlot != 0) return explicitSlot < 0 ? explicitSlot : -2;

            symbol[0] = owner;
            symbol[1] = row;
            bool final = (MethodDefinitions.ReadColumn(source, sourceLayout, row, 2) & 0x20) != 0;
            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            bool hidden = false;
            for (int i = count - 1; i >= 0; i--)
            {
                int type = ancestors[i];
                int targetOwner = AssemblyOf(objects, type), typeRow = TypeDef(objects, type);
                byte[] image = bundle.Get(targetOwner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                explicitSlot = VirtualSlotMetadata.HasNamedMethodImpl(image, heaps, layout, typeRow,
                    source, sourceHeaps, name);
                if (explicitSlot != 0) return explicitSlot < 0 ? explicitSlot : -2;

                if (hidden) continue;
                int status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, typeRow, range);
                if (status < 0) return status;
                int selected = 0, flags = 0;
                for (int candidate = range[0]; candidate < range[1]; candidate++)
                {
                    int attributes = MethodDefinitions.ReadColumn(image, layout, candidate, 2);
                    if ((attributes & 0x50) != 0x40) continue; // Instance virtual.
                    int candidateName = MethodDefinitions.ReadColumn(image, layout, candidate, 3);
                    if (!AssemblyReferences.SameString(source, sourceHeaps, name, image, heaps, candidateName)) continue;
                    int blob = MethodDefinitions.ReadColumn(image, layout, candidate, 4);
                    int match = BundleSignatures.Match(bundle, source, sourceLayout, sourceHeaps, owner,
                        signature, image, layout, heaps, targetOwner, blob, false, objects);
                    if (match < 0) return match;
                    if (match == 0) continue;
                    if (selected != 0) return -5;
                    selected = candidate;
                    flags = attributes;
                }
                if (selected == 0) continue;
                if ((flags & 7) != 6) return -2;
                // A new slot shadows the old name for later ordinary
                // overrides. It does not change calls to the original slot.
                if ((flags & 0x100) != 0)
                {
                    hidden = true;
                    continue;
                }
                if (final) return -2;
                symbol[0] = targetOwner;
                symbol[1] = selected;
                final = (flags & 0x20) != 0;
            }
            return 0;
        }
    }
}
