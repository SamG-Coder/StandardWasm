using PeLibrary;

namespace PortableCompiler
{
    internal static class RuntimeTypeTokens
    {
        // Static field owners are nonnegative. EventArgs.Empty has owner -1.
        // TypeDef rows, closed layouts and primitive vectors use disjoint keys.
        internal const int ClosedTypeKey = 0x1000000, PrimitiveVectorKey = 0x2000000;
        internal static bool IsDescriptorOwner(int owner) => owner <= -2;
        internal static int ModuleSlot(int owner) => -owner - 2;

        internal static bool LowerExactHandleGetter(byte[] image, int[] layout, int[] heaps, int[] records,
            int slot, int[] clauses)
        {
            int kind = records[slot], opcode = records[slot + 3];
            if (kind != BranchIl.Call && kind != BranchIl.MemberCall ||
                opcode != (kind == BranchIl.Call ? 1 : 0x6f)) return false;
            // The two preceding CLI instructions each have a four-byte token.
            // Linking already resolved the token to a non-null descriptor.
            int conversion = slot - 5 * BranchIl.Width, token = slot - 10 * BranchIl.Width;
            if (token < 0 || records[token] != BranchIl.LoadTypeToken ||
                records[conversion] != BranchIl.TypeFromHandle ||
                records[token + 2] * BranchIl.Width != conversion ||
                records[conversion + 2] * BranchIl.Width != slot ||
                ValueOperands.Entry(records, conversion / BranchIl.Width, clauses) ||
                ValueOperands.Entry(records, slot / BranchIl.Width, clauses) ||
                !RuntimeTypeMetadata.IsTypeHandleGetter(image, heaps, layout, records[slot + 1], kind == BranchIl.Call))
                return false;
            records[slot] = BranchIl.TypeToHandle;
            return true;
        }

        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, int[] globals, int context, int[] objects, int[] methodArguments)
        {
            var identity = new int[2];
            int status = ReadIdentity(bundle, image, layout, heaps, owner, coded, context, objects, methodArguments, identity);
            if (status < 0) return status;
            // Module slot zero is the canonical platform System.Object.
            return BindIdentity(globals, identity[0], identity[1]);
        }

        internal static int ReadIdentity(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, int context, int[] objects, int[] methodArguments, int[] identity)
        {
            if ((coded & 3) == 2)
            {
                int type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                    coded >> 2, context, objects, methodArguments);
                return type < 0 ? type : ReadClosedIdentity(bundle, objects, type, identity);
            }
            if (PlatformTypeReferences.IsString(image, heaps, layout, coded))
                return FindCoreType(bundle, "String", identity);
            if ((coded & 3) == 0 && !RuntimeTypeMetadata.ValidDefinition(image, heaps, coded >> 2)) return -1;
            return AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
        }

        internal static int BindLayout(BundleImages bundle, int[] objects, int type, int[] globals)
        {
            var identity = new int[2];
            int status = ReadClosedIdentity(bundle, objects, type, identity);
            return status < 0 ? status : BindIdentity(globals, identity[0], identity[1]);
        }

        internal static int BindIdentity(int[] globals, int owner, int row) =>
            StaticFields.Register(globals, -3 - owner, row, ObjectLayouts.ObjectReference);

        internal static int FindCoreType(BundleImages bundle, string name, int[] identity, string space = "System")
        {
            int found = -1;
            var layout = new int[4];
            var heaps = new int[8];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                // ReadCounts leaves absent tables untouched. Each image needs
                // a fresh table map, including when CoreLib follows a facade.
                var counts = new int[45];
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0) return -1;
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) continue;
                for (int row = 1; row <= counts[2]; row++)
                {
                    if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row, space, name)) continue;
                    if (found >= 0) return -5;
                    found = owner;
                    identity[0] = owner;
                    identity[1] = row;
                }
            }
            return found < 0 ? -6 : 0;
        }

        internal static int ReadClosedIdentity(BundleImages bundle, int[] objects, int type, int[] identity, int depth = 0)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            if (type == SignatureTypes.String) return FindCoreType(bundle, "String", identity);
            if (type == ObjectLayouts.ObjectReference || GenericScalar.IsVector(type))
            {
                // Slot zero names the platform module when CoreLib is present.
                // Keep the CLI vector element key even when storage is shared.
                identity[0] = AssemblyBundle.ObjectOwner;
                identity[1] = type == ObjectLayouts.ObjectReference ? 0 : PrimitiveVectorKey + GenericScalar.Element(type);
                return 0;
            }
            if (GenericScalar.IsScalar(type))
            {
                int found = BundleConstraints.FindPrimitive(bundle, type, identity);
                return found == 0 ? -2 : found < 0 ? found : 0;
            }
            if (ObjectLayouts.IsEnumKey(type))
            {
                ObjectLayouts.EnumIdentity(objects, type, identity);
                return 0;
            }
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0]) return -2;
            int element = ObjectLayouts.ArrayElementKey(objects, type);
            if (element != 0)
            {
                if (element == RuntimeTypeMetadata.Type)
                {
                    int linked = FindCoreType(bundle, "Type", identity);
                    if (linked < 0) return linked;
                    int nominal = ObjectLayouts.Find(objects, identity[0], identity[1], new int[0]);
                    int declared = nominal == 0 ? 0 : ObjectLayouts.FindArray(objects, nominal);
                    if (declared == 0) return -2;
                    // Both physical representations describe the exact CLR Type[].
                    identity[1] = ClosedTypeKey + declared;
                    return 0;
                }
                // The array is nominally distinct from its element but has
                // the element's defining module, including for nested vectors.
                int status = ReadClosedIdentity(bundle, objects, element, identity, depth + 1);
                if (status < 0) return status;
                identity[1] = ClosedTypeKey + type;
                return 0;
            }
            return ReadLayoutIdentity(objects, type, identity);
        }

        internal static int ReadLayoutIdentity(int[] objects, int type, int[] identity)
        {
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                ObjectLayouts.ArrayElementKey(objects, type) != 0) return -2;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            int definition = ObjectLayouts.TypeDef(objects, type);
            if (owner < 0 || definition <= 0) return -2;
            identity[0] = owner;
            identity[1] = ObjectLayouts.HasArguments(objects, type) ? ClosedTypeKey + type : definition;
            return 0;
        }
    }
}
