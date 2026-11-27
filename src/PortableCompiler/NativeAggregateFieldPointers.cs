using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // An exact pointer accessor over a native frame aggregate projects the
    // field of the actual frame cell. NativeFrameWasm owns that cell and its lifetime.
    internal static class NativeAggregateFieldPointers
    {
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records,
            int[] clauses, int[] frame, int parameters)
        {
            int callerType = 0;
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || records[at + 1] < parameters) continue;
                int type = frame[records[at + 1]];
                if (!ObjectLayouts.IsValueType(objects, type)) continue;
                int call = records[at + 2] * Width;
                if (call >= records.Length || ValueOperands.Entry(records, call / Width, clauses)) continue;
                if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, caller, false);
                if (callerType < 1) continue;
                int offset = BorrowOffset(bundle, image, layout, heaps, owner, callerType, context, methodArguments,
                    objects, globals, records, call, type);
                if (offset >= 0) LowerCall(records, call, offset);
            }
        }

        internal static void LowerCall(int[] records, int call, int offset)
        {
            records[call] = NativeFieldPointer;
            records[call + 1] = offset;
            records[call + 3] = 0;
        }

        internal static int BorrowOffset(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int callerType, int context, int[] methodArguments, int[] objects, int[] globals, int[] records, int call,
            int type)
        {
            int row = records[call + 1], targetOwner = owner, targetContext = 0;
            if (records[call] == MemberCall && records[call + 3] == 0x28)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0 ||
                    !ReturnsPointer(image, heaps, reference[5])) return -2;
                var symbol = new int[7];
                if (AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, row, 0x28, false,
                    objects, symbol, context, callerType, callerMethodArguments: methodArguments) < 0) return -2;
                targetOwner = symbol[0];
                row = symbol[1];
                targetContext = symbol[3];
                image = bundle.Get(targetOwner);
                layout = new int[4];
                heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -2;
            }
            else if (records[call] == Call && records[call + 3] == 0)
                targetContext = ObjectLayouts.HasArguments(objects, type) ? type : 0;
            else return -2;

            return FieldOffset(bundle, image, layout, heaps, targetOwner, row, targetContext, type, objects, globals);
        }

        private static bool ReturnsPointer(byte[] image, int[] heaps, int signature)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, signature, 1, cursor);
            return end > cursor[0] && image[cursor[0]] == PointerStorage.Element;
        }

        private static int FieldOffset(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int context, int type, int[] objects, int[] globals)
        {
            if (owner != ObjectLayouts.AssemblyOf(objects, type)) return -2;
            int definition = ObjectLayouts.TypeDef(objects, type);
            if (ObjectLayouts.Owner(image, heaps, layout, row, false) != definition ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0 &&
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x100 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) != 0x10 ||
                TypeInitializers.Find(image, layout, heaps, definition, new int[1]) != 0) return -2;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (!ReturnsPointer(image, heaps, signature)) return -2;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 17 || body[2] < 1 || body[3] != 0 || body[5] != 0) return -2;
            uint start = (uint)body[0];
            // ldarg.0; ldflda field; call AsRef<nint>; call AsPointer<nint>; ret.
            if (U16(image, start) != 0x7c02 || image[start + 6] != 0x28 ||
                image[start + 11] != 0x28 || image[start + 16] != 0x2a) return -2;
            var shape = new int[2];
            if (SignatureTypes.Read(bundle, image, heaps, signature, shape, 0, 0, layout, objects, owner, context) != 1 ||
                shape[0] != Native || shape[1] != ValueLocation(type)) return -2;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !IsNativeWordCall(image, layout, heaps, counts, U32(image, start + 7), true) ||
                !IsNativeWordCall(image, layout, heaps, counts, U32(image, start + 12), false)) return -2;

            return NativeWordOffset(bundle, image, layout, heaps, owner, context, type, objects, globals,
                U32(image, start + 2), write: false);
        }

        internal static int NativeWordOffset(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int type, int[] objects, int[] globals, uint token, bool write)
        {
            int definition = ObjectLayouts.TypeDef(objects, type);
            int field = (int)(token & 0xffffff);
            if (token >> 24 == 4)
            {
                if (ObjectLayouts.Owner(image, heaps, layout, field, true) != definition ||
                    ObjectLayouts.FieldType(objects, type, field) != Native) return -2;
            }
            else if (token >> 24 == 0x0a)
            {
                var linked = new int[4];
                if (AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner, field, write, false,
                    objects, globals, linked, context, definition, constructorFieldWrite: write) < 0 ||
                    FieldAccess.Owner(linked[1]) != type || FieldAccess.Storage(linked[1]) != Native) return -2;
                field = linked[2];
            }
            else return -2;

            var native = new int[2];
            return NativeValueLayouts.Field(bundle, owner, definition, field, native, scopedFrame: true) == 0 && native[1] == 0x4d
                ? native[0] : -2;
        }

        private static bool IsNativeWordCall(byte[] image, int[] layout, int[] heaps, int[] counts,
            uint token, bool readOnlyReference)
        {
            if (token >> 24 != 0x2b) return false;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, (int)(token & 0xffffff), spec) < 0)
                return false;
            int at = spec[MethodSpecifications.Arguments];
            return spec[MethodSpecifications.Arity] == 1 && spec[MethodSpecifications.End] == at + 1 &&
                image[at] == 0x18 &&
                NativeAddressIntrinsics.Identify(image, layout, heaps, counts, spec, readOnlyReference) == NativeAddressView;
        }
    }
}
