using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ExactManagedReads
    {
        // A same-type Unsafe.As immediately followed by ldobj reads the frame
        // value itself. No reference escapes and no bytes are reinterpreted.
        internal static void FoldExactManagedReads(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] frame, int[] records, int[] clauses)
        {
            for (int address = 0; address < records.Length; address += Width)
            {
                if (records[address] != FrameAddress) continue;
                int type = frame[records[address + 1]];
                int call = records[address + 2] * Width;
                if (call >= records.Length) continue;
                if (type == Native)
                {
                    FoldPointerAccessor(image, heaps, layout, records, clauses, address, call);
                    continue;
                }
                if (!ObjectLayouts.IsValueType(objects, type) ||
                    records[call] != MethodSpecCall || records[call + 3] != 0) continue;
                int load = records[call + 2] * Width;
                if (load >= records.Length || records[load] != LoadObject ||
                    ValueOperands.Entry(records, call / Width, clauses) || ValueOperands.Entry(records, load / Width, clauses)) continue;

                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) continue;
                var spec = new int[MethodSpecifications.Width];
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[call + 1], spec) < 0 ||
                    spec[MethodSpecifications.Arity] != 2 ||
                    !IsPlatformAs(image, heaps, layout, counts, spec[MethodSpecifications.Target])) continue;
                var arguments = new int[2];
                if (MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                    context, objects, methodArguments, spec, arguments) < 0 || arguments[0] != type || arguments[1] != type) continue;

                int token = records[load + 1], table = token >> 24;
                if (table != 1 && table != 2 && table != 0x1b) continue;
                int coded = (token & 0xffffff) << 2 | (table == 1 ? 1 : table == 2 ? 0 : 2);
                var localIdentity = new int[2];
                var loadIdentity = new int[2];
                if (RuntimeTypeTokens.ReadLayoutIdentity(objects, type, localIdentity) < 0 ||
                    RuntimeTypeTokens.ReadIdentity(bundle, image, layout, heaps, owner, coded,
                        context, objects, methodArguments, loadIdentity) < 0 ||
                    localIdentity[0] != loadIdentity[0] || localIdentity[1] != loadIdentity[1]) continue;

                records[address] = Load;
                records[call] = Nop;
                records[load] = Nop;
            }
        }

        private static void FoldPointerAccessor(byte[] image, int[] heaps, int[] layout, int[] records,
            int[] clauses, int address, int call)
        {
            int kind = records[call];
            bool member = kind == MemberCall;
            if ((!member && kind != Call) || records[call + 3] != (member ? 0x28 : 0) ||
                ValueOperands.Entry(records, call / Width, clauses) ||
                !NativePointerMetadata.IsPointerAccessor(image, heaps, layout, records[call + 1], member)) return;

            // The accessor only reads the pointer bits already in the slot.
            // Do this before planning native storage for any other address uses.
            records[address] = Load;
            records[call] = Nop;
        }

        private static bool IsPlatformAs(byte[] image, int[] heaps, int[] layout, int[] counts, int target)
        {
            int row = target >> 1;
            int signature, name;
            if ((target & 1) == 0)
            {
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2,
                    "System.Runtime.CompilerServices", "Unsafe")) return false;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    !SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                        "System.Runtime.CompilerServices", "Unsafe")) return false;
                signature = member[5];
                name = member[4];
            }
            if (signature < 1 || signature >= heaps[1] || !MatchesAscii(image, heaps, (uint)name, "As")) return false;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, next);
            int start = next[0];
            // static ref TTo As<TFrom,TTo>(ref TFrom)
            return size == 9 && size <= limit - start && U32(image, (uint)start) == 0x10010210 &&
                U32(image, (uint)(start + 4)) == 0x1e10011e && image[start + 8] == 0;
        }
    }
}
