using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class BooleanStorageAliases
    {
        internal static int Lower(BundleImages bundle, int owner, int[] objects,
            int source, int wrapper, int[] records, int call, int[] clauses)
        {
            if (source != GenericScalar.Key(2) || !IsValueType(objects, wrapper) ||
                IsAutoValue(objects, wrapper) || IsExplicitValue(objects, wrapper) || IsInlineArray(objects, wrapper) ||
                IsByRefLikeValue(objects, wrapper) || HasArguments(objects, wrapper) ||
                AssemblyOf(objects, wrapper) != owner || FieldCount(objects, wrapper) != 1 ||
                FieldTypeAt(objects, wrapper, 0) != Unsigned8)
                return 0;

            int row = FirstRow(objects, wrapper);
            if (ValueFieldProjections.Bind(bundle, objects, wrapper, row, Unsigned8) != BooleanLocation) return 0;

            int field = Next(records, call, clauses);
            if (field < 0) return -2;
            // The store may push one local, argument or constant as its value.
            // This bounds the alias lifetime and prevents an intervening call
            // or control-flow edge from observing the wrapper address.
            bool write = records[field] == Load || records[field] == Constant;
            if (write) field = Next(records, field, clauses);
            if (field < 0 || records[field] != (write ? SetField : GetField) ||
                records[field + 3] != 0 || records[field + 1] != row)
                return -2;

            // Default sequential layout and one exact Boolean field put it
            // at byte zero. Keep the underlying managed location and let the
            // indirect operation read or update its original owner.
            records[call] = ReinterpretManagedLocation;
            records[call + 1] = BooleanLocation;
            records[call + 3] = BooleanLocation;
            records[field] = write ? StoreIndirect : LoadIndirect;
            records[field + 1] = write ? 0x52 : 0x47; // stind.i1 or ldind.u1
            return 1;
        }

        private static int Next(int[] records, int slot, int[] clauses)
        {
            int next = records[slot + 2];
            return next < records.Length / Width && !ValueOperands.Entry(records, next, clauses) ? next * Width : -1;
        }
    }
}
