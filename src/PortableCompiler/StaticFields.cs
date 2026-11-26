using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticFields
    {
        internal const int EventOwner = -1;
        private const int Width = 3;

        internal static int Owner(int[] globals, int index) => globals[1 + index * Width];
        internal static int Storage(int[] globals, int index) => globals[3 + index * Width];

        internal static int Register(int[] globals, int owner, int row, int storage)
        {
            int count = globals[0];
            for (int index = 0; index < count; index++)
                if (Owner(globals, index) == owner && globals[2 + index * Width] == row)
                    return index;

            int slot = 1 + count * Width;
            if (slot > globals.Length - Width) return -3;
            globals[slot] = owner;
            globals[slot + 1] = row;
            globals[slot + 2] = storage;
            globals[0] = count + 1;
            return count;
        }

        internal static int Bind(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int declaringType, int field, int table, int flags, bool write,
            int[] objects, int[] globals, int[] result)
        {
            int status = StaticFieldMetadata.Check(image, heaps, layout, field, flags, write);
            if (status < 0) return status;
            status = TypeInitializers.RejectDeclaringType(image, layout, heaps, declaringType);
            if (status < 0) return status;
            // A constructed generic needs separate static data for each instantiation.
            status = GenericParameters.Check(image, heaps, layout, declaringType, new int[0], objects, new int[0]);
            if (status < 0) return status;
            int storage = BundleLayouts.ReadFieldStorage(bundle, image, layout, heaps, owner,
                table, field, 0, objects, new int[2], true);
            if (storage < 0) return storage;
            if ((!IsScalar(storage) && !IsReference(storage)) || ObjectLayouts.IsValueType(objects, storage)) return -2;

            int index = Register(globals, owner, field, storage);
            if (index < 0) return index;
            result[0] = index;
            result[1] = storage;
            return 0;
        }

        internal static int ResolveLocal(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int row, bool write, int[] objects, int[] globals, int[] result)
        {
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, true);
            if (declaring < 1) return -1;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return table;
            var definition = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, definition);
            return Bind(bundle, image, layout, heaps, owner, declaring, row, table, definition[0],
                write, objects, globals, result);
        }
    }
}
