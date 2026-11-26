using PeLibrary;

namespace PortableCompiler
{
    internal static class BundleLayouts
    {
        internal static int Register(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects)
        {
            int known = ObjectLayouts.Find(objects, owner, row);
            if (known != 0) return known;
            var shape = new int[3];
            int type = ObjectLayouts.Reserve(image, heaps, layout, objects, owner, row, shape);
            if (type < 0) return type;
            var reference = new int[1];
            for (int offset = 0; offset < shape[2]; offset++)
            {
                int storage = ObjectLayouts.ReadStorage(image, heaps, layout, shape[0], shape[1] + offset, reference);
                if (storage < 0) return storage;
                // The layout is visible before recursion, including to a field
                // reached through another assembly in the same bundle.
                if (reference[0] != 0)
                    storage = RegisterReference(main, bundle, ranges, image, layout, heaps, owner, reference[0], objects);
                if (storage < 0) return storage;
                ObjectLayouts.SetFieldType(objects, type, offset, storage);
            }
            return type;
        }

        internal static int RegisterReference(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects)
        {
            if ((coded & 3) == 0)
                return Register(main, bundle, ranges, image, layout, heaps, owner, coded >> 2, objects);
            int platformType = EventMetadata.Reference(image, heaps, layout, coded);
            if (platformType != 0) return platformType;

            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
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
