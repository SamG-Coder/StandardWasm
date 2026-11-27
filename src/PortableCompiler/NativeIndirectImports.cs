using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NativeIndirectImports
    {
        // Keep this parser result outside the Wasm scalar and nominal type ranges.
        // It selects the dynamic-export calling contract, whose result is I32.
        internal const int DynamicUInt32 = 0x40000000;

        internal static int RegisterDynamic(NativeImportTable imports)
        {
            int existing = imports.Find(-2, 2);
            if (existing >= 0) return existing;
            byte[] module = NativeImportTable.Ascii("standardwasm:native-indirect");
            byte[] entry = NativeImportTable.Ascii("platform:u32(i32,p32)");
            return imports.Add(-2, 2, module, module.Length, entry, entry.Length, new byte[] { 0x7f, 0x7f, 0x7f }, 3, 0x7f);
        }

        internal static int RegisterScopedUtf16(NativeImportTable imports)
        {
            int existing = imports.Find(-2, 3);
            if (existing >= 0) return existing;
            byte[] module = NativeImportTable.Ascii("standardwasm:native-indirect");
            byte[] entry = NativeImportTable.Ascii("platform:u32(i32,p32):scoped-utf16");
            return imports.Add(-2, 3, module, module.Length, entry, entry.Length, new byte[] { 0x7f, 0x7f, 0x7f, 0x7f }, 4, 0x7f);
        }

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

        internal static int RegisterCookieLookup(NativeImportTable imports)
        {
            int existing = imports.Find(-2, 4);
            if (existing >= 0) return existing;
            byte[] module = NativeImportTable.Ascii("standardwasm:com");
            byte[] entry = NativeImportTable.Ascii("stdcall:i32(p32,u32,p32,p32)");
            return imports.Add(-2, 4, module, module.Length, entry, entry.Length,
                new byte[] { 0x7f, 0x7f, 0x7f, 0x7f, 0x7f }, 5, 0x7f);
        }

        // The host signatures preserve HRESULT-like wrappers as nominal managed
        // types. The stdcall parameter shape is checked before Wasm retyping.
        // Zero means a different convention; malformed blobs and unsupported
        // stdcall shapes remain distinct diagnostic statuses.
        internal static int ReadResult(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int blob, int owner, int[] objects, int[] nativeArity)
        {
            if (blob < 1 || blob >= heaps[1]) return -1;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (length < 1 || length > limit - start) return -1;
            int convention = image[start];
            if (convention != 0x02 && convention != 0x09) return 0;
            int end = start + length;
            int count = CompressedUnsigned(image, start + 1, end, next);
            int at = next[0];
            if (count < 0 || at >= end) return -1;
            if (convention == 0x09)
            {
                // Platform-default unmanaged call with a scalar and an explicit
                // linear-memory pointer. Resolve the target independently of
                // the managed site that loaded or cached the export token.
                if (count != 2 || end - at < 4 || image[at] != 0x09 || image[at + 1] != 0x08 ||
                    image[at + 2] != PointerStorage.Element) return -2;
                int storage = PointerSignatures.ReadStorage(bundle, image, layout, heaps, owner, at + 3, end, next);
                if (storage < 0) return storage;
                return storage == Native && next[0] == end ? DynamicUInt32 : -2;
            }
            if (count != 1 && count != 4) return -2;

            int element = image[at++], coded = 0;
            if (element == 0x11)
            {
                coded = CompressedUnsigned(image, at, end, next);
                if (coded < 1 || coded >> 2 == 0 || (coded & 3) > 1) return -1;
                at = next[0];
            }
            else if (element != 0x08 && element != 0x09) return -2;

            next[0] = at;
            int receiver = ReadPointer(bundle, image, heaps, layout, owner, next, end, allowNativeWord: count == 1);
            if (receiver < 0) return receiver;
            if (count == 4)
            {
                if (!SignatureReader.TakeElement(image, next, end, 0x09)) return -2;
                int identifier = ReadPointer(bundle, image, heaps, layout, owner, next, end, allowNativeWord: false);
                if (identifier < 0) return identifier;
                if (!SignatureReader.TakeElement(image, next, end, PointerStorage.Element) ||
                    !SignatureReader.TakeElement(image, next, end, PointerStorage.Element) ||
                    !SignatureReader.TakeElement(image, next, end, 1)) return -2;
            }
            if (next[0] != end) return -2;
            nativeArity[0] = count;
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

        private static int ReadPointer(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int owner, int[] cursor, int end, bool allowNativeWord)
        {
            if (cursor[0] >= end) return -1;
            int element = image[cursor[0]++];
            if (allowNativeWord && (element == 0x18 || element == 0x19)) return 0;
            if (element != PointerStorage.Element) return -2;
            int storage = PointerSignatures.ReadStorage(bundle, image, layout, heaps, owner, cursor[0], end, cursor);
            return storage == Native ? 0 : storage < 0 ? storage : -2;
        }
    }
}
