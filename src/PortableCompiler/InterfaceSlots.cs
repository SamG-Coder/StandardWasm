using PeLibrary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // Resolve a nominal interface slot through the receiver class hierarchy.
    internal static class InterfaceSlots
    {
        internal static int SelectBody(BundleImages bundle, int[] objects,
            int receiver, int declared, int owner, int row, int[] symbol, bool primitiveMap = false)
        {
            if (!primitiveMap && !Implements(objects, receiver, declared)) return -2;

            byte[] declaration = bundle.Get(owner);
            var declarationLayout = new int[4];
            var declarationHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(declaration, declarationLayout, declarationHeaps) < 0) return -1;
            int name = MethodDefinitions.ReadColumn(declaration, declarationLayout, row, 3);
            int signature = MethodDefinitions.ReadColumn(declaration, declarationLayout, row, 4);

            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            int declaredContext = HasArguments(objects, declared) ? declared : 0;
            // Primitive maps are proved by the TypeDef's interface metadata.
            // Value layouts do not populate the class assignability graph.
            bool hasMap = primitiveMap;
            for (int type = receiver; type >= FirstClass; type = Parent(objects, type))
            {
                int targetOwner = AssemblyOf(objects, type);
                int targetType = TypeDef(objects, type);
                byte[] image = bundle.Get(targetOwner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int bodyContext = HasArguments(objects, type) ? type : 0;
                int body = ExplicitInterfaceSlots.Find(bundle, objects, image, layout, heaps, targetOwner,
                    targetType, owner, row, declaration, declarationLayout, declarationHeaps, name, signature,
                    declaredContext, bodyContext);
                if (body < 0) return body;
                if (body != 0)
                {
                    symbol[0] = targetOwner;
                    symbol[1] = body;
                    if (symbol.Length > 2) symbol[2] = bodyContext;
                    return 0;
                }

                // A subclass can hide a public method without remapping the
                // interface. Start name-based selection at the closest map.
                hasMap |= DeclaresInterface(objects, type, declared);
                if (!hasMap) continue;

                int status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, targetType, range);
                if (status < 0) return status;
                int selected = 0, selectedFlags = 0;
                for (int method = range[0]; method < range[1]; method++)
                {
                    int flags = MethodDefinitions.ReadColumn(image, layout, method, 2);
                    if ((flags & 0x17) != 6) continue; // public instance member
                    int methodName = MethodDefinitions.ReadColumn(image, layout, method, 3);
                    if (!AssemblyReferences.SameString(declaration, declarationHeaps, name, image, heaps, methodName))
                        continue;
                    int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
                    int match = BundleSignatures.Match(bundle, declaration, declarationLayout, declarationHeaps, owner,
                        signature, image, layout, heaps, targetOwner, blob, false, objects, declaredContext, bodyContext);
                    if (match < 0) return match;
                    if (match == 0) continue;
                    if (selected != 0) return -5;
                    selected = method;
                    selectedFlags = flags;
                }

                if (selected == 0) continue;
                // A final virtual implementation is already a concrete slot
                // target. Only an overridable slot needs class dispatch.
                if ((selectedFlags & 0x60) == 0x40 && !IsValueType(objects, receiver))
                    return VirtualSlots.SelectVirtualBody(bundle, objects, receiver, true, type, targetOwner, selected, symbol);
                symbol[0] = targetOwner;
                symbol[1] = selected;
                if (symbol.Length > 2) symbol[2] = bodyContext;
                return 0;
            }
            if (primitiveMap || IsValueType(objects, receiver)) return -2;
            int flagsOfDeclaration = MethodDefinitions.ReadColumn(declaration, declarationLayout, row, 2);
            if ((flagsOfDeclaration & 0x2450) != 0x40 ||
                MethodDefinitions.ReadColumn(declaration, declarationLayout, row, 0) <= 0) return -2;

            // The CLR gives class mappings priority. At this point the
            // declaration is a default candidate only if every applicable
            // interface map is present and no derived interface fills its slot.
            for (int type = receiver; type >= FirstClass; type = Parent(objects, type))
            {
                int complete = CheckInterfaceMap(bundle, objects, type, layout, heaps);
                if (complete != 0) return complete;
            }
            for (int type = FirstClass; type - FirstClass < objects[0]; type++)
            {
                if (!IsInterface(objects, type) || !Implements(objects, receiver, type)) continue;
                int complete = CheckInterfaceMap(bundle, objects, type, layout, heaps);
                if (complete != 0) return complete;
                if (type == declared || !Implements(objects, type, declared)) continue;

                int targetOwner = AssemblyOf(objects, type), targetType = TypeDef(objects, type);
                int bodyContext = HasArguments(objects, type) ? type : 0;
                int overrideBody = ExplicitInterfaceSlots.Find(bundle, objects, bundle.Get(targetOwner), layout, heaps,
                    targetOwner, targetType, owner, row, declaration, declarationLayout, declarationHeaps,
                    name, signature, declaredContext, bodyContext);
                if (overrideBody != 0) return overrideBody < 0 ? overrideBody : -2;
            }

            symbol[0] = owner;
            symbol[1] = row;
            // A selected default carries its declaring interface even when it has no type arguments.
            if (symbol.Length > 2) symbol[2] = declared;
            return 0;
        }

        private static int CheckInterfaceMap(BundleImages bundle, int[] objects, int type, int[] layout, int[] heaps)
        {
            byte[] image = bundle.Get(AssemblyOf(objects, type));
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var table = new int[3];
            int count = InterfaceImplementations.ReadTable(image, heaps, layout, table);
            if (count < 0) return count;
            int owner = TypeDef(objects, type), declared = 0;
            for (int i = 0; i < count; i++)
            {
                int coded = InterfaceImplementations.Read(image, table, i, owner);
                if (coded < 0) return coded;
                if (coded != 0) declared++;
            }
            return declared == DeclaredInterfaceCount(objects, type) ? 0 : -2;
        }
    }
}
