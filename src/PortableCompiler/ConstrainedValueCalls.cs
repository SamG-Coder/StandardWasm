using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ConstrainedValueCalls
    {
        // The constrained token identifies the exact value at the managed
        // receiver location. A concrete implementation keeps that location live and
        // never boxes a copy. Inherited Object bodies still require boxing.
        internal static int Resolve(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int typeToken, int methodToken, int context, int[] methodArguments, int[] objects, int[] target)
        {
            int table = typeToken >> 24, row = typeToken & 0xffffff;
            int type, element = 0;
            if (table == 0x1b)
                type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, row, context, objects, methodArguments);
            else if (table == 1 || table == 2)
            {
                int coded = row << 2 | (table == 1 ? 1 : 0);
                element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, coded);
                type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, true);
            }
            else return -2;
            if (type < 0) return type;
            bool int32Receiver = type == GenericScalar.Key(SignatureReader.Int32) || element == SignatureReader.Int32;
            bool valueReceiver = IsValueType(objects, type) && !IsExplicitValue(objects, type);
            if (!int32Receiver && !valueReceiver) return -2;

            var declaration = new int[3];
            int status = ResolveDeclaration(bundle, image, layout, heaps, owner, methodToken,
                context, methodArguments, objects, declaration, allowInterface: true);
            if (status < 0) return status;
            int declarationOwner = declaration[0], declarationRow = declaration[1], declared = declaration[2];
            if (int32Receiver)
            {
                int primitive = declared == ObjectReference
                    ? PrimitiveReceivers.ResolveConstrainedInt32Equals(bundle, objects, declarationOwner, declarationRow, target)
                    : PrimitiveReceivers.ResolveConstrainedInt32Compare(bundle, objects, declared, declarationOwner, declarationRow, target);
                if (primitive != -2) return primitive;
            }
            if (!valueReceiver) return -2;
            if (declared != ObjectReference && !Implements(objects, type, declared))
            {
                // Value layouts do not eagerly load interfaces. Bind the map only
                // when a constrained call needs it, with this closed value context.
                int valueOwner = AssemblyOf(objects, type);
                byte[] valueImage = bundle.Get(valueOwner);
                var valueLayout = new int[4];
                var valueHeaps = new int[8];
                if (CompressedTables.FindWithHeaps(valueImage, valueLayout, valueHeaps) < 0) return -1;
                status = BundleLayouts.RegisterInterfaces(bundle, valueImage, valueLayout, valueHeaps, valueOwner, type, objects);
                if (status < 0) return status;
            }

            int selected = VirtualSlots.SelectVirtualBody(bundle, objects, type, true, declared,
                declarationOwner, declarationRow, target, exactValue: true);
            if (selected < 0) return selected;
            if (target[0] == declarationOwner && target[1] == declarationRow) return -2;
            target[2] = HasArguments(objects, type) ? ~type : -1;
            return ValueLocation(type);
        }

        internal static int ResolveObjectDeclaration(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methodToken, int context, int[] methodArguments, int[] objects, int[] declaration) =>
            ResolveDeclaration(bundle, image, layout, heaps, owner, methodToken, context, methodArguments,
                objects, declaration, allowInterface: false);

        private static int ResolveDeclaration(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methodToken, int context, int[] methodArguments, int[] objects, int[] declaration,
            bool allowInterface)
        {
            int declarationOwner = owner, declarationRow = methodToken & 0xffffff;
            int declared = 0;
            if (methodToken >> 24 == 0x0a)
            {
                var member = new int[6];
                int status = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, declarationRow, 0x6f,
                    false, objects, member, context, 0, allowVirtual: true, callerMethodArguments: methodArguments);
                if (status < 0) return status;
                declarationOwner = member[0];
                declarationRow = member[1];
                declared = member[3];
                image = bundle.Get(declarationOwner);
                layout = new int[4];
                heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            }
            else if (methodToken >> 24 != 6) return -2;

            int declaredType = Owner(image, heaps, layout, declarationRow, false);
            if (declaredType < 1) return -1;
            int flags = MethodDefinitions.ReadColumn(image, layout, declarationRow, 2);
            if ((flags & 0x77) == 0x46 && IsCoreObjectType(image, heaps, layout, declaredType << 2))
                declared = ObjectReference;
            else
            {
                if (!allowInterface || (flags & 0x50) != 0x40) return -2;
                if (declared == 0)
                    declared = BundleLayouts.Register(bundle, image, layout, heaps, declarationOwner, declaredType, objects);
                if (declared < 0) return declared;
                if (!IsInterface(objects, declared)) return -2;
            }
            declaration[0] = declarationOwner;
            declaration[1] = declarationRow;
            if (allowInterface) declaration[2] = declared;
            return 0;
        }
    }
}
