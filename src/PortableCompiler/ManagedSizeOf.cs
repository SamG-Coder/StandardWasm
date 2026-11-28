using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ManagedSizeOf
    {
        private const int NativeBytes = 4; // The selected CLR reference pack and Wasm target are both 32-bit.

        internal static int Read(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int token, int context, int[] objects, int[] methodArguments)
        {
            int table = (int)((uint)token >> 24);
            int row = token & 0xffffff;
            if (table == 0x1b)
            {
                int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
                if (blob <= 0 || blob >= heaps[1]) return blob < 0 ? blob : -1;
                int limit = heaps[0] + heaps[1];
                var next = new int[1];
                int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
                int start = next[0];
                if (length < 1 || length > limit - start) return -1;
                int end = start + length;
                int element = image[start];
                if (element == 0x0f || element == 0x1b)
                    return SignatureExtents.Type(image, start, end) == end ? NativeBytes : -1;

                // Resolve VAR and MVAR before measuring. The CLI sizes use
                // nominal primitive widths, even when several types share i32.
                int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner,
                    next, end, context, objects, 0, methodArguments: methodArguments);
                if (type < 0) return type;
                if (next[0] != end) return -1;
                if (GenericScalar.IsScalar(type)) return NativeValueLayouts.PrimitiveSize(GenericScalar.Element(type));
                if (SimdVectors.IsVector(type)) return SimdVectors.Bytes(type);
                if (IsEnumKey(type))
                {
                    int storage = SignatureStorage(objects, type);
                    return storage == I64 ? 8 : storage == I32 ? 4 :
                        storage == Signed16 || storage == Unsigned16 ? 2 : 1;
                }
                if (IsValueType(objects, type)) return Measure(bundle, AssemblyOf(objects, type), TypeDef(objects, type));
                return IsObjectReference(type) || GenericScalar.IsVector(type) ? NativeBytes : -2;
            }
            if (table != 1 && table != 2) return -2;

            int coded = row << 2 | (table == 1 ? 1 : 0);
            int primitive = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, coded);
            if (primitive > 0) return NativeValueLayouts.PrimitiveSize(primitive);
            var identity = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
            return status < 0 ? status : Measure(bundle, identity[0], identity[1]);
        }

        private static int Measure(BundleImages bundle, int owner, int row)
        {
            var measured = new int[2];
            int status = NativeValueLayouts.Measure(bundle, owner, row, measured);
            if (status != -2) return status < 0 ? status : measured[0];

            // A fieldless explicit value exposes its declared extent to sizeof.
            // NativeValueLayouts still diagnoses byte-addressed access to it.
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var shape = new int[5];
            int read = ObjectLayouts.ReadShape(image, heaps, layout, row, shape, valueType: true);
            if (read < 0) return read;
            if (read != 0 || (shape[4] & 0x18) != 0x10) return status;
            int size = ExplicitValueLayouts.ReadFieldlessSize(image, heaps, layout, row, shape[0], shape[1], shape[2]);
            return size != 0 ? size : status;
        }
    }
}
