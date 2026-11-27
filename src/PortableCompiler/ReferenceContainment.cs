using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ReferenceContainment
    {
        private const string RuntimeHelpersNamespace = "System.Runtime.CompilerServices";
        private const string RuntimeHelpersName = "RuntimeHelpers";

        internal static bool IsIntrinsic(byte[] image, int[] layout, int[] heaps, int[] counts, int[] spec)
        {
            if (spec[MethodSpecifications.Arity] != 1) return false;
            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int name, signature;
            if ((target & 1) == 0)
            {
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2, RuntimeHelpersNamespace, RuntimeHelpersName))
                    return false;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member, RuntimeHelpersNamespace, RuntimeHelpersName))
                    return false;
                name = member[4];
                signature = member[5];
            }

            if (!MatchesAscii(image, heaps, (uint)name, "IsReferenceOrContainsReferences") || signature < 1 || signature >= heaps[1])
                return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, next);
            // static bool IsReferenceOrContainsReferences<T>()
            return size == 4 && next[0] <= limit - size && U32(image, (uint)next[0]) == 0x02000110;
        }

        // Use CLI layout and identity. Wasm GC also represents value aggregates
        // as structs, which does not make them managed references in the CLR.
        internal static int Check(int[] objects, int type, int depth = 0)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            if (ObjectLayouts.IsValueType(objects, type))
            {
                int fields = ObjectLayouts.DeclaredFieldCount(objects, type);
                for (int i = 0; i < fields; i++)
                {
                    int storage = ObjectLayouts.FieldTypeAt(objects, type, i);
                    if (storage == 0) continue; // Static fields have no instance storage.
                    int result = Check(objects, storage, depth + 1);
                    if (result != 0) return result;
                }
                return 0;
            }

            int lowered = GenericScalar.Storage(type);
            if (IsScalar(lowered) || ObjectLayouts.IsEnumKey(type) || lowered == NullPointer) return 0;
            if (lowered >= ObjectLayouts.FirstClass && lowered - ObjectLayouts.FirstClass >= objects[0]) return -2;
            if (IsReference(lowered) || IsManagedLocation(lowered) || IsSpan(lowered)) return 1;
            return -2;
        }
    }
}
