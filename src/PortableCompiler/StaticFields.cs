using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticFields
    {
        internal const int EventOwner = -1;
        internal const int Width = 4;

        internal static int Owner(int[] globals, int index) => globals[1 + index * Width];
        internal static int Row(int[] globals, int index) => globals[2 + index * Width];
        internal static int Storage(int[] globals, int index) => globals[3 + index * Width];
        internal static int Context(int[] globals, int index) => globals[4 + index * Width];
        internal static void UseInt32Cell(int[] globals, int index) =>
            globals[3 + index * Width] = AddressedFields.Int32Cell;

        internal static bool IsReferenceCell(int storage) => storage < AddressedFields.Int32Cell;

        internal static int CellVector(int storage) =>
            storage == AddressedFields.Int32Cell ? Ints : AddressedFields.ReferenceCell - storage;

        internal static int UseReferenceCell(int[] globals, int index, int type, int[] objects,
            int[] records, int slot, int action)
        {
            int vector = ObjectLayouts.RegisterArray(objects, ObjectLayouts.ObjectReference);
            if (vector < 0) return vector;
            globals[3 + index * Width] = AddressedFields.ReferenceCell - vector;
            records[slot] = BranchIl.StaticReference;
            records[slot + 1] = index;
            records[slot + 3] = action;
            // Field-token operand bytes have no IL entry, so their records can carry cell metadata.
            records[slot + BranchIl.Width + 1] = type;
            records[slot + BranchIl.Width + 2] = vector;
            return 0;
        }

        internal static int Register(int[] globals, int owner, int row, int storage, int context = 0)
        {
            int count = globals[0];
            for (int index = 0; index < count; index++)
                if (Owner(globals, index) == owner && Row(globals, index) == row && Context(globals, index) == context)
                    return index;

            int slot = 1 + count * Width;
            if (slot > globals.Length - Width) return -3;
            globals[slot] = owner;
            globals[slot + 1] = row;
            globals[slot + 2] = storage;
            globals[slot + 3] = context;
            globals[0] = count + 1;
            return count;
        }

        internal static int Bind(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int declaringType, int field, int table, int flags, bool write,
            int[] objects, int[] globals, int[] result, int context = 0)
        {
            int status = StaticFieldMetadata.Check(image, heaps, layout, field, flags, write);
            if (status < 0) return status;
            // The declaring type's initializer runs before each access (decision 0057).
            // result[2..4] receive its module, .cctor row and TypeDef, or zero.
            int initializer = TypeInitializers.Find(image, layout, heaps, declaringType, new int[1]);
            if (initializer < 0) return initializer;
            if (initializer != 0 && result.Length < 5) return -2;
            // A constructed generic needs separate static data for each instantiation.
            if (context == 0)
            {
                status = GenericParameters.Check(image, heaps, layout, declaringType, new int[0], objects, new int[0]);
                if (status != 0) return status < 0 ? status : -2;
            }
            var reference = new int[3];
            int storage = BundleLayouts.ReadFieldStorage(bundle, image, layout, heaps, owner,
                table, field, context, objects, reference, true, (flags & 0x20) != 0 ? initializer : 0);
            if (storage < 0) return storage;
            // Value layouts use immutable aggregates. The global holds the
            // current whole value, and loads keep an independent snapshot.
            if (!IsScalar(storage) && !IsReference(storage)) return -2;

            int index = Register(globals, owner, field, storage, context);
            if (index < 0) return index;
            result[0] = index;
            result[1] = storage;
            if (result.Length >= 5)
            {
                result[2] = initializer == 0 ? 0 : owner;
                result[3] = initializer;
                result[4] = initializer == 0 ? 0 : declaringType;
            }
            if (result.Length > 5) result[5] = reference[2];
            return 0;
        }

        internal static int ResolveLocal(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int row, bool write, int[] objects, int[] globals, int[] result, int callerRow, int context)
        {
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, true);
            if (declaring < 1) return -1;
            // ECMA-335 II.16.1.2: the declaring type's own initializer may write an
            // initonly static field. Every other write stays rejected.
            if (write && MethodNames.IsInitializer(image, layout, heaps, callerRow) &&
                ObjectLayouts.Owner(image, heaps, layout, callerRow, false) == declaring)
                write = false;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return table;
            var definition = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, definition);
            int declaringContext = context != 0 && ObjectLayouts.TypeDef(objects, context) == declaring ? context : 0;
            return Bind(bundle, image, layout, heaps, owner, declaring, row, table, definition[0],
                write, objects, globals, result, declaringContext);
        }
    }
}
