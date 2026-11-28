using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Redirect a direct call only when every use of its scoped receiver can
    // use the caller's original native stack slot without escaping that slot.
    internal static class ScopedNativeReceivers
    {
        private const int ReadsField = 1, BorrowsField = 2;

        internal static int[] FindCalls(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records,
            int[] clauses, int[] frame, int parameters)
        {
            int[] sites = null;
            bool[] promoted = null;
            int callerType = 0;
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != FrameAddress || records[at + 1] < parameters) continue;
                int local = records[at + 1], type = frame[local];
                if (!ObjectLayouts.IsByRefLikeValue(objects, type)) continue;
                int call = Following(records, at, clauses);
                if (call < 0) continue;
                if (records[call] != NativeFieldPointer &&
                    (records[call] != MemberCall || records[call + 3] != 0x28) &&
                    (records[call] != Call || records[call + 3] != 0)) continue;
                if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, caller, false);
                if (callerType < 1) continue;

                int proof = records[call] == NativeFieldPointer ? BorrowsField :
                    ProveCall(bundle, image, layout, heaps, owner, callerType, context, methodArguments, objects, globals,
                        records, call, type);
                if (proof == 0) continue;
                if (sites == null)
                {
                    sites = new int[records.Length / Width];
                    promoted = new bool[frame.Length];
                }
                sites[call / Width] = local + 1;
                if (proof == BorrowsField) promoted[local] = true;
            }
            if (sites != null)
            {
                // A field read alone can keep the ordinary managed receiver.
                // One real borrow selects the native owner for all proven calls.
                for (int pc = 0; pc < sites.Length; pc++)
                    if (sites[pc] != 0 && !promoted[sites[pc] - 1]) sites[pc] = 0;
            }
            return sites;
        }

        private static int ProveCall(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int callerType, int context, int[] methodArguments, int[] objects, int[] globals, int[] records, int call,
            int type)
        {
            int row = records[call + 1];
            int targetOwner = ObjectLayouts.AssemblyOf(objects, type);
            int definition = ObjectLayouts.TypeDef(objects, type);
            int targetContext = ObjectLayouts.HasArguments(objects, type) ? type : 0;
            if (records[call] == MemberCall)
            {
                var symbol = new int[7];
                if (AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, row, 0x28, false, objects, symbol,
                    context, callerType, callerMethodArguments: methodArguments) < 0 ||
                    symbol[0] != targetOwner || symbol[3] != targetContext || symbol[6] != definition) return 0;
                row = symbol[1];
                if (targetOwner != owner)
                {
                    image = bundle.Get(targetOwner);
                    layout = new int[4];
                    heaps = new int[8];
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return 0;
                }
            }
            else if (targetOwner != owner || targetContext != 0 && targetContext != context) return 0;

            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int access = flags & 7;
            if (records[call] == Call && access != 6 && access != 3 && access != 5 &&
                (access != 1 || callerType != definition)) return 0;
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if (ObjectLayouts.Owner(image, heaps, layout, row, false) != definition ||
                ObjectLayouts.IsConstructor(image, heaps, layout, row) || (flags & ~0x0887) != 0 ||
                (implementation != 0 && implementation != 0x100)) return 0;
            var signature = new int[2];
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (SignatureTypes.Read(bundle, image, heaps, blob, signature, 0, type, layout, objects, targetOwner,
                targetContext) != 1) return 0;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] == 0 || body[1] > 256 || body[2] == 0 || body[5] != 0) return 0;
            int locals = 0;
            if (body[3] != 0)
            {
                int count = CompressedTables.FindWithHeaps(image, layout, heaps);
                if (count < 0) return 0;
                int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, body[3]);
                if (localBlob < 0) return 0;
                locals = SignatureTypes.Read(bundle, image, heaps, localBlob, new int[256], 0, -1, layout, objects,
                    targetOwner, targetContext);
                if (locals < 0) return 0;
            }
            var noClauses = new int[0];
            var decoded = new int[body[1] * Width];
            if (Decode(image, body[0], body[1], 1, locals, decoded, noClauses, false) < 0) return 0;

            int proof = 0;
            for (int at = 0; at < decoded.Length; at += Width)
            {
                int kind = decoded[at];
                if (decoded[at + 1] != 0) continue;
                if (kind == FrameAddress || kind == Store) return 0;
                if (kind != Load) continue;
                int next = Following(decoded, at, noClauses);
                if (next < 0) return 0;
                if (decoded[next] == GetField && image[body[0] + next / Width] == 0x7b)
                {
                    uint token = (uint)decoded[next + 1] | (decoded[next + 3] != 0 ? 0x0a000000u : 0x04000000u);
                    if (NativeAggregateFieldPointers.NativeWordOffset(bundle, image, layout, heaps, targetOwner,
                        targetContext, type, objects, globals, token, write: false) < 0) return 0;
                    if (proof == 0) proof = ReadsField;
                    continue;
                }

                if (!ConsumesBorrow(decoded, next, noClauses) ||
                    NativeAggregateFieldPointers.BorrowOffset(bundle, image, layout, heaps, targetOwner,
                        definition, targetContext, null, objects, globals, decoded, next, type) < 0) return 0;
                proof = BorrowsField;
            }
            return proof;
        }

        internal static void LowerThisBorrow(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int type, int context, int[] methodArguments, int[] objects, int[] globals, int[] records, int[] clauses)
        {
            int definition = ObjectLayouts.TypeDef(objects, type);
            for (int at = 0; at < records.Length; at += Width)
            {
                if (records[at] != Load || records[at + 1] != 0) continue;
                int call = Following(records, at, clauses);
                if (call < 0 || !ConsumesBorrow(records, call, clauses)) continue;
                int offset = NativeAggregateFieldPointers.BorrowOffset(bundle, image, layout, heaps, owner, definition,
                    context, methodArguments, objects, globals, records, call, type);
                if (offset >= 0) NativeAggregateFieldPointers.LowerCall(records, call, offset);
            }
        }

        private static bool ConsumesBorrow(int[] records, int call, int[] clauses)
        {
            int value = Following(records, call, clauses);
            if (value < 0) return false;
            if (records[value] == LoadIndirect) return records[value + 1] == 0x4d;
            // The field pointer may sit below a constant during an indirect
            // store. Neither the borrowed address nor native this escapes.
            if (records[value] != Constant) return false;
            int convert = Following(records, value, clauses);
            if (convert < 0 || records[convert] != ConvertInteger ||
                (records[convert + 1] != 0xd3 && records[convert + 1] != 0xe0)) return false;
            int store = Following(records, convert, clauses);
            return store >= 0 && records[store] == StoreIndirect && records[store + 1] == 0xdf;
        }

        private static int Following(int[] records, int at, int[] clauses)
        {
            int next = records[at + 2] * Width;
            return next > at && next < records.Length && !ValueOperands.Entry(records, next / Width, clauses)
                ? next : -1;
        }
    }
}
