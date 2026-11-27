using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class VirtualSlots
    {
        internal static int SelectVirtualBody(BundleImages bundle, int[] objects,
            int actual, bool allocated, int declared, int owner, int row, int[] symbol, bool finalizer = false, bool exactValue = false)
        {
            int receiver = actual == NullValue ? declared : actual;
            if (receiver == String) return SelectStringBody(bundle, objects, declared, owner, row, symbol);
            if (allocated && declared == ObjectReference && !finalizer &&
                (receiver == ObjectReference || ArrayElementKey(objects, receiver) != 0))
            {
                // Bare objects have no overrides. CLR vector arrays inherit the Object slot.
                symbol[0] = owner;
                symbol[1] = row;
                if (symbol.Length > 2) symbol[2] = 0;
                return 0;
            }
            if (receiver < FirstClass || receiver - FirstClass >= objects[0] ||
                (!allocated && !IsSealed(objects, receiver)) ||
                IsValueType(objects, receiver) != exactValue || IsInterface(objects, receiver)) return -2;

            if (IsInterface(objects, declared))
                return InterfaceSlots.SelectBody(bundle, objects, receiver, declared, owner, row, symbol);

            // The canonical System.Object has no nominal layout in the class chain.
            bool rootDeclaration = declared == ObjectReference;
            int stop = rootDeclaration ? 0 : declared;
            var ancestors = new int[objects[0]];
            int count = 0;
            for (int type = receiver; type != stop; type = Parent(objects, type))
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
            int explicitSlot = rootDeclaration ? 0 : HasClassMethodImpl(bundle, objects, declared, owner, source, sourceHeaps,
                sourceLayout, source, sourceHeaps, name, false);
            if (explicitSlot != 0) return explicitSlot < 0 ? explicitSlot : -2;

            int declaredContext = HasArguments(objects, declared) ? declared : 0;
            symbol[0] = owner;
            symbol[1] = row;
            if (symbol.Length > 2) symbol[2] = declaredContext;
            int declarationFlags = MethodDefinitions.ReadColumn(source, sourceLayout, row, 2);
            int declarationAccess = declarationFlags & 7;
            bool familySlot = declarationAccess == 4 || declarationAccess == 5;
            bool final = (declarationFlags & 0x20) != 0;
            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            bool hidden = false;
            for (int i = count - 1; i >= 0; i--)
            {
                int type = ancestors[i];
                int targetOwner = AssemblyOf(objects, type), typeRow = TypeDef(objects, type);
                int targetContext = HasArguments(objects, type) ? type : 0;
                byte[] image = bundle.Get(targetOwner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                explicitSlot = HasClassMethodImpl(bundle, objects, type, targetOwner, image, heaps, layout,
                    source, sourceHeaps, name, rootDeclaration, finalizer);
                if (explicitSlot < 0) return explicitSlot;
                if (explicitSlot > 0)
                {
                    if (!finalizer || final) return -2;
                    symbol[0] = targetOwner;
                    symbol[1] = explicitSlot;
                    if (symbol.Length > 2) symbol[2] = targetContext;
                    final = (MethodDefinitions.ReadColumn(image, layout, explicitSlot, 2) & 0x20) != 0;
                }

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
                        signature, image, layout, heaps, targetOwner, blob, false, objects, declaredContext,
                        declaredContext != 0 || targetContext != 0 ? targetContext : -1);
                    if (match < 0) return match;
                    if (match == 0) continue;
                    if (selected != 0) return -5;
                    selected = candidate;
                    flags = attributes;
                }
                if (selected == 0) continue;
                int access = flags & 7;
                if (access != 6 && !(familySlot && (access == 4 || access == 5))) return -2;
                // A new slot shadows the old name for later ordinary
                // overrides. It does not change calls to the original slot.
                if ((flags & 0x100) != 0)
                {
                    hidden = true;
                    continue;
                }
                if (explicitSlot > 0) continue;
                if (final) return -2;
                symbol[0] = targetOwner;
                symbol[1] = selected;
                if (symbol.Length > 2) symbol[2] = targetContext;
                final = (flags & 0x20) != 0;
            }
            return 0;
        }

        // System.String is sealed but uses a compiler-owned GC layout. Find its
        // original managed method in CoreLib without registering its desktop fields.
        private static int SelectStringBody(BundleImages bundle, int[] objects, int declared, int owner, int row,
            int[] symbol)
        {
            if (declared != ObjectReference && declared != String) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int selected = row;
            if (declared == ObjectReference)
            {
                var identity = new int[2];
                int status = RuntimeTypeTokens.FindCoreType(bundle, "String", identity);
                if (status < 0) return status == -6 ? -2 : status;
                // The root declaration and the sealed override are in the same CoreLib.
                if (identity[0] != owner) return -2;
                int name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                var range = new int[2];
                status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, identity[1], range);
                if (status < 0) return status;
                selected = 0;
                for (int candidate = range[0]; candidate < range[1]; candidate++)
                {
                    int flags = MethodDefinitions.ReadColumn(image, layout, candidate, 2);
                    if ((flags & 0x150) != 0x40) continue; // Instance virtual reusing a slot.
                    int candidateName = MethodDefinitions.ReadColumn(image, layout, candidate, 3);
                    if (!AssemblyReferences.SameString(image, heaps, name, image, heaps, candidateName)) continue;
                    int candidateSignature = MethodDefinitions.ReadColumn(image, layout, candidate, 4);
                    int match = BundleSignatures.Match(bundle, image, layout, heaps, owner, signature,
                        image, layout, heaps, owner, candidateSignature, false, objects);
                    if (match < 0) return match;
                    if (match == 0) continue;
                    if (selected != 0) return -5;
                    selected = candidate;
                }
                if (selected == 0) return -2;
            }
            symbol[0] = owner;
            symbol[1] = selected;
            if (symbol.Length > 2) symbol[2] = 0;
            return 0;
        }

        private static int HasClassMethodImpl(BundleImages bundle, int[] objects, int type, int owner,
            byte[] image, int[] heaps, int[] layout, byte[] declaration, int[] declarationHeaps, int name,
            bool rootDeclaration, bool finalizer = false)
        {
            var cursor = new int[1];
            var parent = new int[2];
            var implementation = finalizer ? new int[2] : null;
            int typeRow = TypeDef(objects, type);
            while (true)
            {
                int coded = VirtualSlotMetadata.NextNamedMethodImplParent(image, heaps, layout, typeRow,
                    declaration, declarationHeaps, name, cursor, implementation);
                if (coded <= 0) return coded;
                if ((coded & 3) == 2)
                {
                    // A MethodImpl on a closed interface cannot replace a class slot.
                    // A closed class remap is still diagnosed at its exact declaration.
                    int declarationType = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                        owner, coded >> 2, type, objects, null);
                    if (declarationType < 0) return declarationType;
                    for (int ancestor = type; ancestor >= FirstClass; ancestor = Parent(objects, ancestor))
                        if (ancestor == declarationType) return finalizer ? -2 : 1;
                    continue;
                }
                int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, parent);
                if (status < 0) return status == -1 ? -1 : -2;

                if (rootDeclaration && parent[0] == AssemblyBundle.ObjectOwner)
                {
                    if (!finalizer) return 1;
                    // C# destructors can explicitly override Object.Finalize in MethodImpl metadata.
                    if (!FinalizerSlots.Signature(image, heaps, implementation[1]) || (implementation[0] & 1) != 0) return -2;
                    int body = implementation[0] >> 1;
                    int declaringType = ObjectLayouts.Owner(image, heaps, layout, body, false);
                    if (declaringType != typeRow || (MethodDefinitions.ReadColumn(image, layout, body, 2) & 0x50) != 0x40 ||
                        !FinalizerSlots.Signature(image, heaps, MethodDefinitions.ReadColumn(image, layout, body, 4))) return -2;
                    return body;
                }

                // An interface MethodImpl can share a name with this virtual
                // slot. Only a declaration on the class chain can remap it.
                for (int current = type; current >= FirstClass; current = Parent(objects, current))
                    if (AssemblyOf(objects, current) == parent[0] && TypeDef(objects, current) == parent[1])
                        return finalizer ? -2 : 1;
            }
        }
    }
}
