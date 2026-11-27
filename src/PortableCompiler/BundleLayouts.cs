using PeLibrary;

namespace PortableCompiler
{
    internal static class BundleLayouts
    {
        internal static int Register(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects)
        {
            return RegisterClosed(bundle, image, layout, heaps, owner, row, objects, new int[0]);
        }

        // A value-type definition in its own assembly. Returns -2 for a
        // reference type or an enum, so callers keep their class diagnostics.
        internal static int RegisterValue(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects)
        {
            int type = RegisterClosed(bundle, image, layout, heaps, owner, row, objects, new int[0], true);
            if (type < 0) return type;
            return ObjectLayouts.IsValueType(objects, type) ? type : -2;
        }

        private static int RegisterClosed(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] arguments, bool valueType = false, bool enumIdentity = false)
        {
            int type = ReserveTree(bundle, image, layout, heaps, owner, row, objects, arguments, valueType);
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0])
                return enumIdentity && type > 0 ? ObjectLayouts.RegisterEnum(objects, owner, row, type) : type;
            return Complete(bundle, image, layout, heaps, owner, type, objects);
        }

        private static int ReserveTree(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] arguments, bool valueType)
        {
            int known = ObjectLayouts.Find(objects, owner, row, arguments);
            if (known != 0) return ObjectLayouts.IsValueType(objects, known) == valueType ? known : -2;
            if (bundle.LayoutDepth >= ObjectLayouts.MaxTypeDepth) return -2;
            bundle.LayoutDepth++;
            int reserved = ReserveNew(bundle, image, layout, heaps, owner, row, objects, arguments, valueType);
            bundle.LayoutDepth--;
            return reserved;
        }

        private static int ReserveNew(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] arguments, bool valueType)
        {
            var shape = new int[5];
            var deferred = new int[8];
            int status = GenericParameters.Check(image, heaps, layout, row, arguments, objects, deferred);
            if (status < 0) return status;
            if (status != 0)
            {
                status = BundleConstraints.Check(bundle, image, layout, heaps, owner, objects, arguments, deferred);
                if (status < 0) return status;
            }
            status = ObjectLayouts.ReadShape(image, heaps, layout, row, shape, valueType);
            if (status != 0) return status;
            int parent = 0;
            if (shape[3] != 0)
            {
                int baseType = shape[3];
                parent = (baseType & 3) == 2
                    ? ManagedReferenceSignatures.ReserveBase(bundle, image, layout, heaps, owner, baseType >> 2, objects, arguments)
                    : ReserveParent(bundle, image, layout, heaps, owner, baseType, objects, new int[0]);
                if (parent < 0) return parent;
                if (parent != 0 && (ObjectLayouts.IsSealed(objects, parent) || ObjectLayouts.IsInterface(objects, parent)))
                    return -2;
                if (parent != 0 && !ObjectLayouts.HasLayout(objects, parent) &&
                    ObjectLayouts.RequiresLayoutParent(image, heaps, layout, shape))
                    return -2;
            }
            // A field of a base argument can re-enter registration and reserve
            // this class after the argument's own nominal identity is available.
            int known = ObjectLayouts.Find(objects, owner, row, arguments);
            if (known != 0)
                return ObjectLayouts.Parent(objects, known) == parent && ObjectLayouts.IsValueType(objects, known) == valueType ? known : -2;

            // Reserve the parent before the child in the Wasm recursive type group.
            return ObjectLayouts.Reserve(objects, owner, row, shape, parent, arguments);
        }

        internal static int ReserveParent(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects, int[] arguments)
        {
            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return arguments.Length == 0 ? 0 : -2;
            if (symbol[0] == owner)
                return ReserveTree(bundle, image, layout, heaps, owner, symbol[1], objects, arguments, false);
            byte[] target = bundle.Get(symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return ReserveTree(bundle, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects, arguments, false);
        }

        private static int Complete(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int type, int[] objects)
        {
            if (!ObjectLayouts.BeginFields(objects, type)) return type;
            if (ObjectLayouts.IsInterface(objects, type) && ObjectLayouts.ArgumentCount(objects, type) == 1)
            {
                int row = ObjectLayouts.TypeDef(objects, type);
                if (SzArrayEnumeration.IsEnumerable(image, layout, heaps, row))
                    ObjectLayouts.MarkArrayEnumerable(objects, type);
                else if (SzArrayEnumeration.IsList(image, layout, heaps, row))
                    ObjectLayouts.MarkArrayList(objects, type);
            }
            int parent = ObjectLayouts.Parent(objects, type);
            if (parent != 0)
            {
                int parentOwner = ObjectLayouts.AssemblyOf(objects, parent);
                int status;
                if (parentOwner == owner)
                    status = Complete(bundle, image, layout, heaps, owner, parent, objects);
                else
                {
                    byte[] parentImage = bundle.Get(parentOwner);
                    var parentLayout = new int[4];
                    var parentHeaps = new int[8];
                    if (CompressedTables.FindWithHeaps(parentImage, parentLayout, parentHeaps) < 0) return -1;
                    status = Complete(bundle, parentImage, parentLayout, parentHeaps, parentOwner, parent, objects);
                }
                if (status < 0) return status;
            }
            if (!ObjectLayouts.IsValueType(objects, type))
            {
                int interfaces = RegisterInterfaces(bundle, image, layout, heaps, owner, type, objects);
                if (interfaces < 0) return interfaces;
            }
            var reference = new int[2];
            int fields = ObjectLayouts.FieldCount(objects, type);
            int first = ObjectLayouts.FirstRow(objects, type);
            int table = ObjectLayouts.TableOf(objects, type);
            if (ObjectLayouts.IsInlineArray(objects, type))
            {
                int element = ReadFieldStorage(bundle, image, layout, heaps, owner,
                    table, first, type, objects, reference, false);
                if (element <= 0) return element == 0 ? -2 : element;

                // Only the backing field has a metadata row. The remaining slots
                // use its resolved storage in this closed generic context.
                for (int offset = 0; offset < fields; offset++)
                    ObjectLayouts.SetFieldType(objects, type, offset, element);
                return type;
            }
            bool overlay = ObjectLayouts.IsExplicitValue(objects, type);
            bool byRefLike = ObjectLayouts.IsByRefLikeValue(objects, type);
            int[] offsets = new int[overlay ? fields : 0];
            int limit = overlay ? ExplicitValueLayouts.Read(image, heaps, layout, ObjectLayouts.TypeDef(objects, type), first, offsets) : 0;
            if (limit == -2)
                return ExplicitSlotLayouts.Bind(bundle, image, layout, heaps, owner, type, objects);
            if (limit < 0) return limit;
            for (int offset = 0; offset < fields; offset++)
            {
                int storage = ReadFieldStorage(bundle, image, layout, heaps, owner,
                    table, first + offset, type, objects, reference, false);
                if (storage < 0) return storage;
                // A supported span carries its owner in the span descriptor.
                // Ordinary nested values use the existing copy representation.
                // The field reader rejects other nested scoped values before this point.
                if (byRefLike && storage != 0 &&
                    !SignatureTypes.IsScalar(storage) && !SignatureTypes.IsReference(storage) &&
                    !SignatureTypes.IsSpan(storage)) return -2;
                if (overlay && storage != 0)
                {
                    int width = ExplicitValueLayouts.ViewWidth(objects, storage);
                    int start = offsets[offset] - 1;
                    if (start < 0) return -1;
                    if (width == 0 || start > limit - width)
                        return ExplicitSlotLayouts.Bind(bundle, image, layout, heaps, owner, type, objects);
                    ObjectLayouts.SetExplicitField(objects, type, offset, storage, start);
                }
                else ObjectLayouts.SetFieldType(objects, type, offset, storage);
            }
            return type;
        }

        internal static int ReadFieldStorage(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int table, int row, int receiver, int[] objects,
            int[] reference, bool staticField, int readonlyInitializer = 0)
        {
            int storage = ObjectLayouts.ReadStorage(image, heaps, layout, table, row, reference, staticField);
            if (storage < 0) return storage;
            if (reference[0] < 0)
            {
                int end = reference[1];
                reference[0] = -reference[0];
                // Stage 164: the same delegate rule as SignatureTypes, so a Func<int> field (a C#
                // cached-lambda field, for example) stores the value its delegate construction made.
                var afterDelegate = new int[1];
                int delegateStorage = image[reference[0]] == 0x15
                    ? SignatureTypes.GenericDelegateStorage(image, heaps, layout, reference[0], end, afterDelegate) : 0;
                if (delegateStorage != 0)
                {
                    reference[0] = afterDelegate[0];
                    storage = delegateStorage;
                }
                else if (image[reference[0]] == PointerStorage.Element)
                {
                    // Native integers are scalar handles. Pointer signatures
                    // can carry an address whose lifetime a scope must preserve.
                    if (ObjectLayouts.IsByRefLikeValue(objects, receiver)) return -2;
                    storage = PointerSignatures.ReadStorage(bundle, image, layout, heaps,
                        owner, reference[0] + 1, end, reference);
                }
                else
                    storage = ManagedReferenceSignatures.ReadStorage(bundle, image, layout, heaps,
                        owner, reference, end, receiver, objects);
                if (storage >= 0 && reference[0] != end) return -1;
            }
            else if (reference[0] != 0 && readonlyInitializer != 0 &&
                RuntimeTypeMetadata.Storage(image, heaps, layout, reference[0], reference[1] == 1) == RuntimeTypeMetadata.Type &&
                RuntimeTypeFields.HasCanonicalInitializer(image, heaps, layout, readonlyInitializer, row))
                storage = RuntimeTypeMetadata.Type;
            else if (reference[0] != 0 && !staticField && bundle.TypeFields.UsesDescriptor(owner, row))
                storage = RuntimeTypeMetadata.Type;
            else if (reference[0] != 0)
            {
                bool valueType = reference[1] == 1;
                storage = RegisterType(bundle, image, layout, heaps, owner,
                    reference[0], objects, valueType);
                // A value-type field needs its layout, so it keeps the -4 (CLR probe 3).
                if (storage == -4 && !valueType && bundle.DeferMissing) storage = ObjectLayouts.ObjectReference;
            }
            if (SignatureTypes.IsSpan(storage))
                return !staticField && ObjectLayouts.IsByRefLikeValue(objects, receiver) ? storage : -2;
            return ObjectLayouts.IsByRefLikeValue(objects, storage) ? -2 : storage;
        }

        internal static int RegisterInterfaces(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int type, int[] objects)
        {
            var table = new int[3];
            int count = InterfaceImplementations.ReadTable(image, heaps, layout, table);
            if (count < 0) return count;
            int row = ObjectLayouts.TypeDef(objects, type);
            for (int index = 0; index < count; index++)
            {
                int coded = InterfaceImplementations.Read(image, table, index, row);
                if (coded < 0) return coded;
                // Missing or unsupported closed interface metadata cannot
                // establish an assignment. Ordinary TypeRef checks are unchanged.
                if (coded == 0 || (coded & 3) != 0 && bundle.Length == 0) continue;
                bool closed = (coded & 3) == 2;
                int target = closed ? ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, coded >> 2, type, objects, null) : RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
                if (target == -4 || target == -6 || closed && target == -2) continue;
                if (target < 0) return target;
                if (!ObjectLayouts.IsInterface(objects, target)) return -2;
                if (!ObjectLayouts.DeclaresInterface(objects, type, target) &&
                    !ObjectLayouts.AddInterface(objects, type, target)) return -3;
            }
            return 0;
        }

        internal static int RegisterInstantiation(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects, int[] arguments, bool valueType)
        {
            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return -2;
            if (symbol[0] == owner)
                return RegisterClosed(bundle, image, layout, heaps, owner, symbol[1], objects, arguments, valueType);
            byte[] target = bundle.Get(symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return RegisterClosed(bundle, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects, arguments, valueType);
        }

        internal static int RegisterReference(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects)
            => RegisterType(bundle, image, layout, heaps, owner, coded, objects, false);

        internal static int RegisterType(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects, bool valueType, bool enumIdentity = false)
        {
            if (!valueType && RuntimeTypeMetadata.IsAssemblyDefinition(image, heaps, layout, coded))
                return RuntimeTypeMetadata.Assembly;
            int platformType = valueType ? 0 : EventMetadata.Reference(image, heaps, layout, coded);
            if (platformType != 0) return platformType;

            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return valueType ? -2 : ObjectLayouts.ObjectReference;
            if (symbol[0] == owner)
            {
                // Stage 204 (decision 0091): a delegate TypeDef is a delegate key, not a class layout.
                int defined = valueType ? 0 : GeneralDelegates.DefinedStorage(bundle, image, layout, heaps, owner, symbol[1], objects);
                if (defined != 0) return defined;
                return RegisterClosed(bundle, image, layout, heaps, owner, symbol[1], objects, new int[0], valueType, enumIdentity);
            }
            byte[] target = bundle.Get(symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            int definedElsewhere = valueType ? 0 : GeneralDelegates.DefinedStorage(bundle, target, targetLayout, targetHeaps,
                symbol[0], symbol[1], objects);
            if (definedElsewhere != 0) return definedElsewhere;
            return RegisterClosed(bundle, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects, new int[0], valueType, enumIdentity);
        }
    }
}
