using PeLibrary;

namespace PortableCompiler
{
    internal static class BundleLayouts
    {
        internal static int Register(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects)
        {
            int type = ReserveTree(main, bundle, ranges, image, layout, heaps, owner, row, objects, 0);
            return type < 0 ? type : Complete(main, bundle, ranges, image, layout, heaps, owner, type, objects);
        }

        private static int ReserveTree(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int depth)
        {
            int known = ObjectLayouts.Find(objects, owner, row);
            if (known != 0) return known;
            if (depth >= ObjectLayouts.MaxClasses) return -2;
            var shape = new int[5];
            int status = ObjectLayouts.ReadShape(image, heaps, layout, row, shape);
            if (status < 0) return status;
            int parent = 0;
            if (shape[3] != 0)
            {
                var symbol = new int[2];
                status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, shape[3], symbol);
                if (status < 0) return status;
                if (symbol[0] == AssemblyBundle.ObjectOwner) parent = 0;
                else if (symbol[0] == owner)
                    parent = ReserveTree(main, bundle, ranges, image, layout, heaps, owner, symbol[1], objects, depth + 1);
                else
                {
                    byte[] target = AssemblyBundle.Load(main, bundle, ranges, symbol[0]);
                    var targetLayout = new int[4];
                    var targetHeaps = new int[8];
                    if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                    parent = ReserveTree(main, bundle, ranges, target, targetLayout, targetHeaps,
                        symbol[0], symbol[1], objects, depth + 1);
                }
                if (parent < 0) return parent;
                if (parent != 0 && (ObjectLayouts.IsSealed(objects, parent) || ObjectLayouts.IsInterface(objects, parent)))
                    return -2;
            }
            // The parent must precede the child in the Wasm recursive type group.
            // Defer all fields until that ordering is fixed, even if a base field
            // refers back to the derived type being registered.
            return ObjectLayouts.Reserve(objects, owner, row, shape, parent);
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
            int interfaces = RegisterInterfaces(main, bundle, ranges, image, layout, heaps, owner, type, objects);
            if (interfaces < 0) return interfaces;
            var reference = new int[1];
            int fields = ObjectLayouts.FieldCount(objects, type);
            int first = ObjectLayouts.FirstRow(objects, type);
            int table = ObjectLayouts.TableOf(objects, type);
            for (int offset = 0; offset < fields; offset++)
            {
                int storage = ObjectLayouts.ReadStorage(image, heaps, layout, table, first + offset, reference);
                if (storage < 0) return storage;
                if (reference[0] != 0)
                    storage = RegisterReference(main, bundle, ranges, image, layout, heaps, owner, reference[0], objects);
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

        internal static int RegisterReference(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects)
        {
            int platformType = EventMetadata.Reference(image, heaps, layout, coded);
            if (platformType != 0) return platformType;

            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return ObjectLayouts.ObjectReference;
            if (symbol[0] == owner)
                return Register(main, bundle, ranges, image, layout, heaps, owner, symbol[1], objects);
            byte[] target = AssemblyBundle.Load(main, bundle, ranges, symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return Register(main, bundle, ranges, target, targetLayout, targetHeaps, symbol[0], symbol[1], objects);
        }
    }
}
