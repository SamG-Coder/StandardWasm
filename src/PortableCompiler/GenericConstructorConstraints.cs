using PeLibrary;

namespace PortableCompiler
{
    internal static class GenericConstructorConstraints
    {
        internal static int Check(BundleImages bundle, int[] objects, int[] arguments, int[] requirements)
        {
            for (int i = 0; i < requirements.Length; i++)
            {
                if (requirements[i] == 0) continue;
                int result = CheckArgument(bundle, objects, arguments[i]);
                if (result < 0) return result;
            }
            return 0;
        }

        private static int CheckArgument(BundleImages bundle, int[] objects, int type)
        {
            // The CLR permits default construction of all value types and
            // System.Object. Other reference types need their own constructor.
            if (type == ObjectLayouts.ObjectReference || GenericScalar.IsScalar(type) ||
                ObjectLayouts.IsEnumKey(type) || ObjectLayouts.IsValueType(objects, type)) return 0;
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                ObjectLayouts.IsAbstract(objects, type) || ObjectLayouts.IsInterface(objects, type) ||
                ObjectLayouts.ArrayElementKey(objects, type) != 0) return -2;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            int declaration = ObjectLayouts.TypeDef(objects, type);
            if (owner < 0 || owner >= bundle.Count || declaration < 1) return -2;

            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            var cursor = new int[1];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int found = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, declaration, range);
            if (found < 0) return found;
            for (int row = range[0]; row < range[1]; row++)
            {
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                if ((flags & 0x17) != 6 || !ObjectLayouts.IsConstructor(image, heaps, layout, row)) continue;
                int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
                int end = SignatureReader.ReadInstance(image, heaps, blob, 0, cursor);
                if (end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) && cursor[0] == end)
                    return 0;
            }
            return -2;
        }
    }
}
