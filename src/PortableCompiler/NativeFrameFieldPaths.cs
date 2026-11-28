using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A local field path can enter linear memory once its complete declaring
    // type chain and native offsets are known. The frame still owns the bytes.
    internal static class NativeFrameFieldPaths
    {
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records,
            int[] clauses, int[] frame, int parameters)
        {
            bool[] native = null;
            var path = new int[4];
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || records[at + 1] < parameters) continue;
                int local = records[at + 1];
                if (!Read(bundle, image, layout, heaps, owner, caller, context, methodArguments, objects,
                    globals, records, clauses, at, frame[local], path, convert: true)) continue;
                int end = path[1];
                if (native == null) native = new bool[frame.Length];
                native[local] = true;
                Collapse(records, path, records[end + 2]);
                records[end] = 0;
            }
            if (native == null) return;
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || !native[records[at + 1]]) continue;
                if (!Read(bundle, image, layout, heaps, owner, caller, context, methodArguments, objects,
                    globals, records, clauses, at, frame[records[at + 1]], path, convert: false)) continue;
                int end = path[1];
                // An ordinary read of the promoted field sees any native writes.
                if (path[3] != 0 && records[end + 1] == path[3])
                    Collapse(records, path, end / Width);
            }
        }

        private static bool Read(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records,
            int[] clauses, int address, int type, int[] path, bool convert)
        {
            int first = Next(records, address);
            if (first < 0 || records[first] != FieldAddress) return false;
            int end = first;
            while (end >= 0 && records[end] == FieldAddress) end = Next(records, end);
            if (end < 0 || (convert ? records[end] != ConvertInteger || records[end + 1] != 0xe0 && records[end + 1] != 0xd3 :
                records[end] != LoadIndirect)) return false;
            int at = first, offset = 0;
            var native = new int[2];
            var linked = new int[4];
            while (at < end)
            {
                if (ValueOperands.Entry(records, at / Width, clauses) || !ObjectLayouts.IsValueType(objects, type) ||
                    ObjectLayouts.IsByRefLikeValue(objects, type) ||
                    ObjectLayouts.IsExplicitValue(objects, type)) return false;
                int assembly = ObjectLayouts.AssemblyOf(objects, type), definition = ObjectLayouts.TypeDef(objects, type);
                int field = records[at + 1], storage;
                if (records[at + 3] == 0)
                {
                    if (assembly != owner || ObjectLayouts.Owner(image, heaps, layout, field, true) != definition ||
                        !ValueOperands.IsWritableInteriorField(image, layout, heaps, field, false)) return false;
                    storage = ObjectLayouts.FieldType(objects, type, field);
                }
                else
                {
                    int callerType = ObjectLayouts.Owner(image, heaps, layout, caller, false);
                    if (callerType < 1 || AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner, field, false,
                        false, objects, globals, linked, context, callerType, methodArguments) < 0 ||
                        FieldAccess.Owner(linked[1]) != type || (linked[3] & 0x20) != 0) return false;
                    field = linked[2];
                    storage = FieldAccess.Storage(linked[1]);
                }
                if (storage == Void || NativeValueLayouts.Field(bundle, assembly, definition, field, native) != 0 ||
                    native[0] > int.MaxValue - offset) return false;
                offset += native[0];
                type = storage;
                at = records[at + 2] * Width;
            }
            if (ValueOperands.Entry(records, end / Width, clauses)) return false;
            path[0] = first;
            path[1] = end;
            path[2] = offset;
            path[3] = native[1];
            return true;
        }

        private static int Next(int[] records, int at)
        {
            int next = records[at + 2] * Width;
            return next > at && next < records.Length ? next : -1;
        }

        private static void Collapse(int[] records, int[] path, int successor)
        {
            int first = path[0];
            for (int at = records[first + 2] * Width; at < path[1]; at = records[at + 2] * Width) records[at] = 0;
            NativeAggregateFieldPointers.LowerCall(records, first, path[2]);
            records[first + 2] = successor;
        }
    }
}
