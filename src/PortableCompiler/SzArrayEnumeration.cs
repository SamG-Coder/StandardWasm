using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // SZ-array interface dispatch executes the runtime's original enumerator
    // factory. Only the two VM operations that know the intrinsic array
    // receiver need a representation bridge.
    internal static class SzArrayEnumeration
    {
        internal static bool IsEnumerable(byte[] image, int[] layout, int[] heaps, int row) =>
            IsCoreType(image, layout, heaps, row, "System.Collections.Generic", "IEnumerable`1");

        internal static bool IsList(byte[] image, int[] layout, int[] heaps, int row) =>
            IsCoreType(image, layout, heaps, row, "System.Collections.Generic", "IList`1") ||
            IsCoreType(image, layout, heaps, row, "System.Collections.Generic", "ICollection`1");

        private static bool IsCoreType(byte[] image, int[] layout, int[] heaps, int row, string space, string name)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                NamedDefinition(image, heaps, layout, counts, first, row, space, name);
        }

        internal static bool IsFactory(byte[] image, int[] layout, int[] heaps, int row)
        {
            int owner = Owner(image, heaps, layout, row, false);
            if (owner < 1 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2050) != 0 ||
                MethodDefinitions.ReadColumn(image, layout, row, 0) == 0) return false;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            return MatchesAscii(image, heaps, name, "InternalArray__IEnumerable_GetEnumerator") &&
                    IsCoreType(image, layout, heaps, owner, "System", "Array") ||
                MatchesAscii(image, heaps, name, "GetEnumerator") &&
                    IsCoreType(image, layout, heaps, owner, "System", "SZArrayHelper");
        }

        internal static bool IsArrayLength(byte[] image, int[] layout, int[] heaps, int row)
        {
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            if (!MatchesAscii(image, heaps, name, "get_Length") ||
                !IsCoreType(image, layout, heaps, Owner(image, heaps, layout, row, false), "System", "Array")) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 0, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) && cursor[0] == end;
        }

        internal static bool IsArrayCast(byte[] image, int[] layout, int[] heaps, int[] counts, int target)
        {
            if ((target & 1) != 0) return false;
            int row = target >> 1;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Runtime.CompilerServices",
                    "Unsafe", "As", new byte[] { 0x10, 1, 1, 0x1e, 0, 0x1c }, true);
        }

        internal static int Bind(BundleImages bundle, int[] objects, int vector, int declared, int owner, int row,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int functions, MethodInstantiations instantiations)
        {
            if (instantiations == null || !IsExactArrayEnumerable(objects, vector, declared) || AssemblyOf(objects, declared) != owner)
                return -2;
            byte[] image = bundle.Get(owner);
            var heaps = new int[8];
            var layout = new int[4];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            if (Owner(image, heaps, layout, row, false) != TypeDef(objects, declared) ||
                !MatchesAscii(image, heaps, name, "GetEnumerator")) return -2;

            var identity = new int[2];
            int found = RuntimeTypeTokens.FindCoreType(bundle, "SZArrayHelper", identity);
            if (found == -6) found = RuntimeTypeTokens.FindCoreType(bundle, "Array", identity);
            if (found < 0 || identity[0] != owner) return found < 0 ? found : -2;
            var range = new int[2];
            int status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, identity[1], range);
            if (status < 0) return status;
            int factory = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                if (!IsFactory(image, layout, heaps, candidate)) continue;
                if (factory != 0) return -5;
                factory = candidate;
            }
            if (factory == 0) return -2;
            var arguments = new[] { ArrayElementKey(objects, vector) };
            int bound = instantiations.Find(owners, rows, contexts, functions, owner, factory, 0, arguments);
            if (bound >= 0) return bound;
            status = MethodCompiler.Register(bundle, image, layout, heaps, owner, factory, 0,
                owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions,
                receiverOverride: vector, methodArguments: arguments);
            if (status < 0) return status;
            instantiations.Record(functions, owners.Length, arguments);
            return functions;
        }
    }
}
