using PeLibrary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal static class ConstrainedValueCalls
    {
        // The constrained token identifies the exact value at the managed
        // receiver location. A direct override keeps that location live and
        // never boxes a copy. Inherited Object bodies still require boxing.
        internal static int Resolve(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int typeToken, int methodToken, int context, int[] methodArguments, int[] objects, int[] target)
        {
            int table = typeToken >> 24, row = typeToken & 0xffffff;
            int type;
            if (table == 0x1b)
                type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, row, context, objects, methodArguments);
            else if (table == 1 || table == 2)
                type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner,
                    row << 2 | (table == 1 ? 1 : 0), objects, true);
            else return -2;
            if (type < 0) return type;
            if (!IsValueType(objects, type) || IsExplicitValue(objects, type)) return -2;

            int declarationOwner = owner, declarationRow = methodToken & 0xffffff;
            if (methodToken >> 24 == 0x0a)
            {
                var member = new int[6];
                int status = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, declarationRow, 0x6f,
                    false, objects, member, context, 0, allowVirtual: true, callerMethodArguments: methodArguments);
                if (status < 0) return status;
                declarationOwner = member[0];
                declarationRow = member[1];
                image = bundle.Get(declarationOwner);
                layout = new int[4];
                heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            }
            else if (methodToken >> 24 != 6) return -2;

            int declaredType = Owner(image, heaps, layout, declarationRow, false);
            if (declaredType < 1) return -1;
            if (!IsCoreObjectType(image, heaps, layout, declaredType << 2) ||
                (MethodDefinitions.ReadColumn(image, layout, declarationRow, 2) & 0x77) != 0x46)
                return -2;

            int selected = VirtualSlots.SelectVirtualBody(bundle, objects, type, true, ObjectReference,
                declarationOwner, declarationRow, target, exactValue: true);
            if (selected < 0) return selected;
            if (target[0] == declarationOwner && target[1] == declarationRow) return -2;
            target[2] = HasArguments(objects, type) ? type : 0;
            return type;
        }
    }
}
