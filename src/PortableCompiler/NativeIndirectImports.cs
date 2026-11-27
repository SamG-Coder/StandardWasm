using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NativeIndirectImports
    {
        // A guest COM vtable slot names a host-issued token. The original
        // pointer load remains in the IL; the host validates the token and receiver.
        internal static int Register(NativeImportTable imports)
        {
            int existing = imports.Find(-2, 1);
            if (existing >= 0) return existing;
            byte[] module = NativeImportTable.Ascii("standardwasm:com");
            byte[] entry = NativeImportTable.Ascii("stdcall:i32(p32)");
            return imports.Add(-2, 1, module, module.Length, entry, entry.Length, new byte[] { 0x7f, 0x7f }, 2, 0x7f);
        }

        // The initial host signature accepts one wasm32 pointer and a four-byte
        // integer result. HRESULT-like wrappers keep their nominal managed type.
        // Zero means a different convention; malformed blobs and unsupported
        // stdcall shapes remain distinct diagnostic statuses.
        internal static int ReadResult(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int blob, int owner, int[] objects)
        {
            if (blob < 1 || blob >= heaps[1]) return -1;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (length < 1 || length > limit - start) return -1;
            if (image[start] != 0x02) return 0;
            int end = start + length;
            int count = CompressedUnsigned(image, start + 1, end, next);
            int at = next[0];
            if (count < 0 || at >= end) return -1;
            if (count != 1) return -2;

            int element = image[at++], coded = 0;
            if (element == 0x11)
            {
                coded = CompressedUnsigned(image, at, end, next);
                if (coded < 1 || coded >> 2 == 0 || (coded & 3) > 1) return -1;
                at = next[0];
            }
            else if (element != 0x08 && element != 0x09) return -2;

            if (at >= end) return -1;
            int parameter = image[at++];
            if (parameter == PointerStorage.Element)
            {
                int storage = PointerSignatures.ReadStorage(bundle, image, layout, heaps, owner, at, end, next);
                if (storage < 0) return storage;
                if (storage != Native) return -2;
                at = next[0];
            }
            else if (parameter != 0x18 && parameter != 0x19) return -2;
            if (at != end) return -2;
            if (coded == 0) return I32;

            int type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, valueType: true);
            if (type < 0) return type;
            if (!IsValueType(objects, type) || IsExplicitValue(objects, type)) return -2;
            bool hasPayload = false;
            for (int i = 0; i < FieldCount(objects, type); i++)
            {
                int storage = FieldTypeAt(objects, type, i);
                if (storage == 0) continue; // Static constants have no Wasm field.
                if (storage != I32 || hasPayload) return -2;
                hasPayload = true;
            }
            if (!hasPayload) return -2;
            var measured = new int[2];
            int status = NativeValueLayouts.Measure(bundle, AssemblyOf(objects, type), TypeDef(objects, type), measured);
            if (status < 0) return status;
            return measured[0] == 4 && measured[1] == 4 ? type : -2;
        }
    }
}
