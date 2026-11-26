using PeLibrary;

namespace PortableCompiler
{
    // Stage 126 (decision 0054): catch clauses that can receive a thrown managed object.
    // A clause mask keeps the fixed-tag bits (ExceptionTypes.CatchMask) and adds
    // ExceptionTypes.Object with the caught class above ExceptionTypes.ClassShift.
    // ObjectReference as the class means "any object" (catch of System.Object).
    internal static class ManagedExceptionClauses
    {
        internal static bool CatchesObjects(int mask) => (mask & ExceptionTypes.Object) != 0;

        internal static int CaughtClass(int mask) => mask >> ExceptionTypes.ClassShift;

        internal static int FixedBits(int mask) => mask & ExceptionTypes.Supported;

        // Returns the clause mask, 0 when the clause stays unsupported, or a negative status.
        internal static int ClauseMask(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int token, int[] objects)
        {
            int fixedBits = ExceptionTypes.CatchMask(image, heaps, layout, token);
            uint table = (uint)token >> 24;
            if (table != 1 && table != 2) return fixedBits;
            int row = token & 0xffffff;
            if (row == 0) return -1;
            int coded = row << 2 | (table == 1 ? 1 : 0); // TypeDefOrRef: TypeDef 0, TypeRef 1
            int type = BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
            // An exception class the bundle cannot lay out can never be thrown as an
            // object here, so the fixed tags alone describe what the clause can receive.
            if (type < 0) return fixedBits;
            if (type != ObjectLayouts.ObjectReference &&
                (type < ObjectLayouts.FirstClass || ObjectLayouts.IsInterface(objects, type) || ObjectLayouts.IsValueType(objects, type)))
                return fixedBits;
            return fixedBits | ExceptionTypes.Object | type << ExceptionTypes.ClassShift;
        }
    }
}
