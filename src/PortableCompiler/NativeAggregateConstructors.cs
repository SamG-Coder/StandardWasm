using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NativeAggregateConstructors
    {
        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records,
            int[] clauses, int[] frame)
        {
            bool[] native = null;
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress) continue;
                int next = records[at + 2] * Width;
                if (next >= records.Length || records[next] != NativeFieldPointer) continue;
                if (native == null) native = new bool[frame.Length];
                native[records[at + 1]] = true;
            }
            if (native == null) return;

            int callerType = ObjectLayouts.Owner(image, heaps, layout, caller, false);
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || !native[records[at + 1]]) continue;
                int argument = Next(records, at, clauses);
                if (argument < 0) continue;
                int call = Next(records, argument, clauses);
                if (call < 0) continue;
                if (records[argument] == Constant)
                {
                    if (records[call] != ConvertInteger || records[call + 1] != 0xd3 && records[call + 1] != 0xe0)
                        continue;
                    call = Next(records, call, clauses);
                    if (call < 0) continue;
                }
                else if (records[argument] != Load || frame[records[argument + 1]] != Native) continue;

                int type = frame[records[at + 1]];
                int offset = ResolveOffset(bundle, image, layout, heaps, owner, callerType, context,
                    methodArguments, objects, globals, records, call, type);
                if (offset < 0) continue;

                // Only the first address and the constructor call are replaced.
                // The original argument still evaluates before the one in-place store.
                records[at] = NativeFrameFieldAddress;
                records[at + 3] = offset;
                records[call] = StoreIndirect;
                records[call + 1] = 0xdf; // stind.i
                records[call + 3] = 0;
            }
        }

        private static int Next(int[] records, int at, int[] clauses)
        {
            int next = records[at + 2] * Width;
            return next > at && next < records.Length && !ValueOperands.Entry(records, next / Width, clauses)
                ? next : -1;
        }

        private static int ResolveOffset(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int callerType, int context, int[] methodArguments, int[] objects, int[] globals,
            int[] records, int call, int type)
        {
            int row = records[call + 1], targetOwner = owner;
            int definition = ObjectLayouts.TypeDef(objects, type);
            int targetContext = ObjectLayouts.HasArguments(objects, type) ? type : 0;
            if (records[call] == MemberCall && records[call + 3] == 0x28)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return -2;
                var symbol = new int[7];
                if (AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, row, 0x28, false, objects, symbol,
                    context, callerType, valueAddress: true, callerMethodArguments: methodArguments) < 0 ||
                    symbol[2] != definition || symbol[3] != targetContext ||
                    symbol[0] != ObjectLayouts.AssemblyOf(objects, type)) return -2;
                targetOwner = symbol[0];
                row = symbol[1];
                if (targetOwner != owner)
                {
                    image = bundle.Get(targetOwner);
                    layout = new int[4];
                    heaps = new int[8];
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -2;
                }
            }
            else if (records[call] == Call && records[call + 3] == 0)
            {
                if (owner != ObjectLayouts.AssemblyOf(objects, type) ||
                    targetContext != 0 && targetContext != context) return -2;
                int access = MethodDefinitions.ReadColumn(image, layout, row, 2) & 7;
                if (access != 6 && access != 3 && access != 5 && (access != 1 || callerType != definition))
                    return -2;
            }
            else return -2;

            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if (ObjectLayouts.Owner(image, heaps, layout, row, false) != definition ||
                !ObjectLayouts.IsConstructor(image, heaps, layout, row) || (flags & ~0x1887) != 0 ||
                (flags & 0x1800) != 0x1800 || (implementation != 0 && implementation != 0x100) ||
                TypeInitializers.Find(image, layout, heaps, definition, new int[1]) != 0) return -2;

            var shape = new int[3];
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (SignatureTypes.Read(bundle, image, heaps, signature, shape, 0, type, layout, objects, targetOwner,
                targetContext) != 2 || shape[0] != Void || shape[2] != Native) return -2;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 8 || body[2] < 2 || body[3] != 0 || body[5] != 0)
                return -2;
            uint start = (uint)body[0];
            // ldarg.0; ldarg.1; stfld field; ret, with no other effects.
            if (U16(image, start) != 0x0302 || image[start + 2] != 0x7d || image[start + 7] != 0x2a)
                return -2;
            return NativeAggregateFieldPointers.NativeWordOffset(bundle, image, layout, heaps, targetOwner,
                targetContext, type, objects, globals, U32(image, start + 3), write: true);
        }
    }
}
