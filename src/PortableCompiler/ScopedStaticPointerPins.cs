using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ScopedStaticPointerPins
    {
        internal const int BorrowedAddress = 1;

        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int method, int methodCount,
            int[] records, int[] clauses, int[] pinned, int[] frame, int parameters, int locals, int[] location)
        {
            int local = -1;
            for (int i = 0; i < locals; i++)
            {
                if (pinned[i] != PinnedLocals.StaticPointer) continue;
                if (local >= 0) return -2;
                local = parameters + i;
            }
            if (local < 0) return 0;
            if (clauses.Length != 0 || !MethodNames.IsInitializer(image, layout, heaps, method)) return -2;

            int address = -1, store = -1, load = -1, clear = -1;
            for (int at = 0; at < records.Length; at = Next(records, at))
            {
                int next = Next(records, at);
                if (next <= at) return -1;
                if (records[at + 1] != local) continue;
                if (records[at] == FrameAddress) return Reject(location, at);
                if (records[at] == Load)
                {
                    if (store < 0 || load >= 0 || clear >= 0) return Reject(location, at);
                    load = at;
                }
                if (records[at] != Store) continue;
                int previous = Previous(records, at);
                if (store < 0 && At(records, previous, StaticFieldAddress, -1) && records[previous + 3] == 0)
                {
                    address = previous;
                    store = at;
                    continue;
                }
                if (load < 0 || clear >= 0 || !At(records, previous, ConvertInteger, 0xe0) ||
                    !At(records, Previous(records, previous), Constant, 0)) return Reject(location, at);
                clear = at;
            }
            if (clear < 0 || Next(records, store) != load) return -2;
            int convert = Next(records, load);
            if (!At(records, convert, ConvertInteger, 0xe0)) return Reject(location, load);
            int use = Next(records, convert), consumer = use;
            if (At(records, use, Store, -1))
            {
                int alias = records[use + 1];
                if (alias < parameters || alias == local || alias >= parameters + locals || frame[alias] != SignatureTypes.Native)
                    return Reject(location, use);
                int aliasLoad = FindAliasLoad(records, use, clear, alias);
                if (aliasLoad < 0) return Reject(location, use);
                consumer = Next(records, aliasLoad);
            }
            int after = NativeStore(records, consumer);
            if (after < 0 && At(records, consumer, Call, -1) && records[consumer + 3] == 0)
            {
                int target = records[consumer + 1];
                if (WritesBorrowedWord(image, layout, heaps, methodCount, target) ||
                    ComOutputPointer.MatchesActivation(bundle, image, layout, heaps, owner, methodCount, target))
                    after = Next(records, consumer);
            }
            if (after < 0 || after >= clear) return Reject(location, consumer);
            // All instructions between taking the address and clearing the pin
            // execute in order. The address reaches only a validated consumer.
            for (int at = store; at <= clear; at = Next(records, at))
                if (TransfersControl(records[at]) || ValueOperands.Entry(records, at / Width, clauses)) return Reject(location, at);
            records[address + Width + 3] = BorrowedAddress;
            return 0;
        }

        private static int FindAliasLoad(int[] records, int store, int clear, int alias)
        {
            int load = -1;
            for (int at = 0; at < records.Length; at = Next(records, at))
            {
                if (at == store || records[at + 1] != alias) continue;
                int kind = records[at];
                if (kind == Store || kind == FrameAddress) return -1;
                if (kind != Load) continue;
                if (load >= 0 || at < store || at > clear) return -1;
                load = at;
            }
            return load;
        }

        private static bool TransfersControl(int kind) => kind == Branch || kind == Conditional || kind == Switch ||
            kind == Return || kind == Leave || kind == ThrowNull || kind == Rethrow || kind == EndFinally;

        private static bool WritesBorrowedWord(byte[] image, int[] layout, int[] heaps, int methods, int target)
        {
            if (target < 1 || target > methods || (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) == 0)
                return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, target, 4), 1, cursor);
            int signature = cursor[0];
            if (end - signature < 4 || image[signature] != 1 || image[signature + 1] != 0x0f ||
                image[signature + 2] != 0x0f || SignatureExtents.Type(image, signature + 1, end) != end) return false;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, target, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] > 24 || body[3] != 0 || body[5] != 0) return false;
            var records = new int[body[1] * Width];
            if (Decode(image, body[0], body[1], 1, 0, records, new int[0], false) < 0 || !At(records, 0, Load, 0)) return false;
            int finish = NativeStore(records, Next(records, 0));
            return At(records, finish, Return, -1) && Next(records, finish) == records.Length;
        }

        private static int NativeStore(int[] records, int at)
        {
            if (!At(records, at, Constant, -1)) return -1;
            int conversion = Next(records, at);
            if (!At(records, conversion, ConvertInteger, 0xd3) && !At(records, conversion, ConvertInteger, 0xe0)) return -1;
            int store = Next(records, conversion);
            return At(records, store, StoreIndirect, 0xdf) ? Next(records, store) : -1;
        }

        private static int Reject(int[] location, int at)
        {
            if (location.Length != 0) location[2] = at / Width;
            return -2;
        }

        private static int Previous(int[] records, int target)
        {
            for (int at = 0; at >= 0 && at < target; at = Next(records, at))
                if (Next(records, at) == target) return at;
            return -1;
        }

        private static int Next(int[] records, int at) =>
            at >= 0 && at < records.Length ? records[at + 2] * Width : -1;

        private static bool At(int[] records, int at, int kind, int operand) =>
            at >= 0 && at < records.Length && records[at] == kind && (operand < 0 || records[at + 1] == operand);
    }
}
