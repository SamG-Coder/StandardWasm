using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // The out slot already lives in linear memory. Preserve the original
    // managed zeroing and size store, and route its generated pin to the leaf.
    internal static class CoreVersionByref
    {
        internal const int ContextKey = int.MaxValue;
        private const int ImportOwner = -5;

        internal static int[] MarkCalls(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int methodCount, int[] objects, int[] records, int[] clauses, int[] frame, int parameters, int[] scoped)
        {
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || records[at + 1] < parameters) continue;
                int call = records[at + 2] * Width;
                if (call >= records.Length || records[call] != Call || records[call + 3] != 0) continue;
                int local = records[at + 1], type = frame[local];
                if (!ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.AssemblyOf(objects, type) != owner) continue;
                int target = records[call + 1];
                if (target < 1 || target > methodCount ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, target, 3), "RtlGetVersionEx") ||
                    ValueOperands.Entry(records, call / Width, clauses)) continue;
                int definition = Validate(bundle, image, layout, heaps, owner, target, new int[1]);
                if (definition == 0 || ObjectLayouts.TypeDef(objects, type) != definition) continue;
                if (scoped == null) scoped = new int[records.Length / Width];
                scoped[call / Width] = local + 1;
                records[call] = CoreVersionOutCall;
            }
            return scoped;
        }

        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int[] records, NativeImportTable imports, int nativeBase, int function)
        {
            var leaf = new int[1];
            if (Validate(bundle, image, layout, heaps, owner, row, leaf) == 0) return -2;
            byte[] module = NativeImportTable.Ascii("standardwasm:windows-version"), name = NativeImportTable.Ascii("rtlGetVersion");
            int import = imports.Add(ImportOwner, leaf[0], module, module.Length, name, name.Length, new byte[] { 0x7f }, 1, 0x7f);
            const int call = 20 * Width;
            records[call] = CallNative;
            records[call + 1] = function;
            records[call + 3] = nativeBase + import;
            return 0;
        }

        private static int Validate(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int wrapper, int[] leaf)
        {
            if (wrapper < 1 || !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, wrapper, 3), "RtlGetVersionEx"))
                return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || wrapper > counts[6] || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            int ownerType = ObjectLayouts.Owner(image, heaps, layout, wrapper, false);
            if (ownerType < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, ownerType, "", "NtDll", nested: true)) return 0;
            int outer = NestedTypes.Enclosing(image, heaps, layout, counts, ownerType);
            if (outer < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, outer, "", "Interop")) return 0;
            int structure = Signature(image, layout, heaps, wrapper, 0x10);
            if (structure < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, structure, "", "RTL_OSVERSIONINFOEX", nested: true) ||
                NestedTypes.Enclosing(image, heaps, layout, counts, structure) != ownerType) return 0;
            var body = new int[6];
            if (!Body(image, layout, wrapper, body, 26)) return 0;
            uint start = (uint)body[0], typeToken = 0x02000000u | (uint)structure;
            if (U16(image, start) != 0xfe02 || image[start + 2] != 0x15 || U32(image, start + 3) != typeToken ||
                U16(image, start + 7) != 0xfe02 || image[start + 9] != 0x1c || U32(image, start + 10) != typeToken ||
                image[start + 14] != 0x7d || U16(image, start + 19) != 0x2802 || image[start + 25] != 0x2a) return 0;
            int sizeField = TokenRow(U32(image, start + 15), 4, counts[4]);
            int pin = TokenRow(U32(image, start + 21), 6, counts[6]);
            if (sizeField == 0 || pin == 0 || ObjectLayouts.Owner(image, heaps, layout, sizeField, true) != structure ||
                ObjectLayouts.Owner(image, heaps, layout, pin, false) != ownerType || Signature(image, layout, heaps, pin, 0x10) != structure ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, pin, 3), "RtlGetVersion") ||
                !Body(image, layout, pin, body, 13) || !PinLocal(image, layout, heaps, counts[6], body[3], structure)) return 0;
            start = (uint)body[0];
            // The generated stub pins its sole byref, converts it to the
            // native ABI, makes a synchronous call, and clears the pin.
            if (U32(image, start) != 0xe0060a02 || image[start + 4] != 0x28 || U32(image, start + 9) != 0x2a0ae016) return 0;
            int target = TokenRow(U32(image, start + 5), 6, counts[6]);
            var import = new int[3];
            if (target == 0 || ObjectLayouts.Owner(image, heaps, layout, target, false) != ownerType ||
                Signature(image, layout, heaps, target, 0x0f) != structure ||
                (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x2010) != 0x2010 ||
                MethodDefinitions.ReadColumn(image, layout, target, 0) != 0 ||
                NativeImports.ForMethod(image, heaps, layout, counts, target, import) != 1 || import[0] != 0x101 ||
                !MatchesAscii(image, heaps, (uint)import[1], "RtlGetVersion") ||
                !MatchesAscii(image, heaps, (uint)import[2], "ntdll.dll")) return 0;
            var measured = new int[2];
            if (NativeValueLayouts.Measure(bundle, owner, structure, measured) != 0 || measured[0] != 276 || measured[1] != 4 ||
                NativeValueLayouts.Field(bundle, owner, structure, sizeField, measured) != 0 || measured[0] != 0 || measured[1] != 0x4b)
                return 0;
            leaf[0] = target;
            return structure;
        }

        private static int Signature(byte[] image, int[] layout, int[] heaps, int row, int pointer)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, blob, 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 8) ||
                !SignatureReader.TakeElement(image, cursor, end, pointer) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return 0;
            int type = CompressedUnsigned(image, cursor[0], end, cursor);
            return type > 0 && (type & 3) == 0 && cursor[0] == end ? type >> 2 : 0;
        }

        private static bool Body(byte[] image, int[] layout, int row, int[] body, int length) =>
            MethodBodies.Read(image, unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0)), body) == 0 &&
            body[1] == length && body[5] == 0;

        private static bool PinLocal(byte[] image, int[] layout, int[] heaps, int methods, int token, int structure)
        {
            if (token == 0) return false;
            int blob = StandaloneSignatures.BlobIndex(image, heaps, layout, methods, token);
            if (blob < 1 || blob >= heaps[1]) return false;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            if (length < 6 || length > limit - start || U32(image, (uint)start) != 0x10450107 || image[start + 4] != 0x11)
                return false;
            return CompressedUnsigned(image, start + 5, start + length, cursor) == structure << 2 &&
                cursor[0] == start + length;
        }

        private static int TokenRow(uint token, int table, int count)
        {
            int row = (int)(token & 0xffffff);
            return token >> 24 == table && row > 0 && row <= count ? row : 0;
        }
    }
}
