using PeLibrary;

namespace PortableCompiler
{
    internal static class BundleLayouts
    {
        internal static int Register(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects)
        {
            return RegisterClosed(main, bundle, ranges, image, layout, heaps, owner, row, objects, new int[0]);
        }

        private static int RegisterClosed(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] arguments, bool valueType = false)
        {
            int type = ReserveTree(main, bundle, ranges, image, layout, heaps, owner, row, objects, arguments, new int[0], 0, valueType);
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0]) return type;
            return Complete(main, bundle, ranges, image, layout, heaps, owner, type, objects);
        }

        private static int ReserveTree(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] arguments, int[] noArguments, int depth, bool valueType)
        {
            int known = ObjectLayouts.Find(objects, owner, row, arguments);
            if (known != 0) return ObjectLayouts.IsValueType(objects, known) == valueType ? known : -2;
            if (depth >= ObjectLayouts.MaxClasses) return -2;
            var shape = new int[5];
            int status = GenericParameters.Check(image, heaps, layout, row, arguments.Length);
            if (status < 0) return status;
            status = ObjectLayouts.ReadShape(image, heaps, layout, row, shape, valueType);
            if (status != 0) return status;
            int parent = 0;
            if (shape[3] != 0)
            {
                var symbol = new int[2];
                status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, shape[3], symbol);
                if (status < 0) return status;
                if (symbol[0] == AssemblyBundle.ObjectOwner) parent = 0;
                else if (symbol[0] == owner)
                    parent = ReserveTree(main, bundle, ranges, image, layout, heaps, owner, symbol[1], objects, noArguments, noArguments, depth + 1, false);
                else
                {
                    byte[] target = AssemblyBundle.Load(main, bundle, ranges, symbol[0]);
                    var targetLayout = new int[4];
                    var targetHeaps = new int[8];
                    if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                    parent = ReserveTree(main, bundle, ranges, target, targetLayout, targetHeaps,
                        symbol[0], symbol[1], objects, noArguments, noArguments, depth + 1, false);
                }
                if (parent < 0) return parent;
                if (parent != 0 && (ObjectLayouts.IsSealed(objects, parent) || ObjectLayouts.IsInterface(objects, parent)))
                    return -2;
            }
            // The parent must precede the child in the Wasm recursive type group.
            // Defer all fields until that ordering is fixed, even if a base field
            // refers back to the derived type being registered.
            return ObjectLayouts.Reserve(objects, owner, row, shape, parent, arguments);
        }

        private static int Complete(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int type, int[] objects)
        {
            if (!ObjectLayouts.BeginFields(objects, type)) return type;
            int parent = ObjectLayouts.Parent(objects, type);
            if (parent != 0)
            {
                int parentOwner = ObjectLayouts.AssemblyOf(objects, parent);
                int status;
                if (parentOwner == owner)
                    status = Complete(main, bundle, ranges, image, layout, heaps, owner, parent, objects);
                else
                {
                    byte[] parentImage = AssemblyBundle.Load(main, bundle, ranges, parentOwner);
                    var parentLayout = new int[4];
                    var parentHeaps = new int[8];
                    if (CompressedTables.FindWithHeaps(parentImage, parentLayout, parentHeaps) < 0) return -1;
                    status = Complete(main, bundle, ranges, parentImage, parentLayout, parentHeaps, parentOwner, parent, objects);
                }
                if (status < 0) return status;
            }
            if (!ObjectLayouts.IsValueType(objects, type))
            {
                int interfaces = RegisterInterfaces(main, bundle, ranges, image, layout, heaps, owner, type, objects);
                if (interfaces < 0) return interfaces;
            }
            var reference = new int[2];
            int fields = ObjectLayouts.FieldCount(objects, type);
            int first = ObjectLayouts.FirstRow(objects, type);
            int table = ObjectLayouts.TableOf(objects, type);
            for (int offset = 0; offset < fields; offset++)
            {
                int storage = ObjectLayouts.ReadStorage(image, heaps, layout, table, first + offset, reference);
                if (storage < 0) return storage;
                if (reference[0] < 0)
                {
                    int end = reference[1];
                    reference[0] = -reference[0];
                    if (image[reference[0]] == PointerStorage.Element)
                        storage = PointerSignatures.ReadStorage(main, bundle, ranges, image, layout, heaps,
                            owner, reference[0] + 1, end, reference);
                    else
                        storage = ManagedReferenceSignatures.Read(main, bundle, ranges, image, layout, heaps,
                            owner, reference, end, type, objects, 0);
                    if (storage >= 0 && reference[0] != end) return -1;
                }
                else if (reference[0] != 0)
                    storage = RegisterType(main, bundle, ranges, image, layout, heaps, owner, reference[0], objects, reference[1] == 1);
                if (storage < 0) return storage;
                ObjectLayouts.SetFieldType(objects, type, offset, storage);
            }
            return type;
        }

        private static int RegisterInterfaces(byte[] main, byte[] bundle, int[] ranges, byte[] image,
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
                // Generic interface instantiations need TypeSpec support.
                // Missing optional assemblies cannot establish an assignment.
                if (coded == 0 || (coded & 3) == 2 || (coded & 3) == 1 && bundle.Length == 0) continue;
                int target = RegisterReference(main, bundle, ranges, image, layout, heaps, owner, coded, objects);
                if (target == -4 || target == -6) continue;
                if (target < 0) return target;
                if (!ObjectLayouts.IsInterface(objects, target)) return -2;
                if (!ObjectLayouts.AddInterface(objects, type, target)) return -3;
            }
            return 0;
        }

        internal static int RegisterInstantiation(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects, int[] arguments)
        {
            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return -2;
            if (symbol[0] == owner)
                return RegisterClosed(main, bundle, ranges, image, layout, heaps, owner, symbol[1], objects, arguments);
            byte[] target = AssemblyBundle.Load(main, bundle, ranges, symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return RegisterClosed(main, bundle, ranges, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects, arguments);
        }

        internal static int RegisterReference(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects)
            => RegisterType(main, bundle, ranges, image, layout, heaps, owner, coded, objects, false);

        internal static int RegisterType(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects, bool valueType)
        {
            int platformType = valueType ? 0 : EventMetadata.Reference(image, heaps, layout, coded);
            if (platformType != 0) return platformType;

            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return valueType ? -2 : ObjectLayouts.ObjectReference;
            if (symbol[0] == owner)
                return RegisterClosed(main, bundle, ranges, image, layout, heaps, owner, symbol[1], objects, new int[0], valueType);
            byte[] target = AssemblyBundle.Load(main, bundle, ranges, symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return RegisterClosed(main, bundle, ranges, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects, new int[0], valueType);
        }
    }
}
