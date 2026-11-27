using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ExactTypeInitialization
    {
        // Only remove the handle producers when the full sequence has one
        // entry and reaches the authentic static helper. Other descriptor uses
        // keep their original effects and verifier categories.
        internal static int MatchCall(byte[] image, int[] layout, int[] heaps, int[] records, int token, int[] clauses)
        {
            int next = records[token + 2] * Width;
            if (next >= records.Length) return -1;
            if (IsInitializerCall(image, layout, heaps, records, next))
                return ValueOperands.Entry(records, next / Width, clauses) ? -1 : next;

            if (!IsTypeCall(image, layout, heaps, records, next, false)) return -1;
            int getter = records[next + 2] * Width;
            if (getter >= records.Length || !IsTypeCall(image, layout, heaps, records, getter, true)) return -1;
            int call = records[getter + 2] * Width;
            if (call >= records.Length || !IsInitializerCall(image, layout, heaps, records, call) ||
                ValueOperands.Entry(records, next / Width, clauses) ||
                ValueOperands.Entry(records, getter / Width, clauses) ||
                ValueOperands.Entry(records, call / Width, clauses)) return -1;
            return call;
        }

        private static bool IsInitializerCall(byte[] image, int[] layout, int[] heaps, int[] records, int slot)
        {
            int kind = records[slot];
            bool definition = kind == Call;
            return (definition || kind == MemberCall) && records[slot + 3] == (definition ? 0 : 0x28) &&
                RuntimeInitializerMetadata.IsRunClassConstructor(image, layout, heaps, records[slot + 1], definition);
        }

        private static bool IsTypeCall(byte[] image, int[] layout, int[] heaps, int[] records, int slot, bool getter)
        {
            int kind = records[slot], row = records[slot + 1];
            bool definition = kind == Call;
            if (!definition && kind != MemberCall) return false;
            int opcode = records[slot + 3];
            bool expectedCall = definition ? opcode == (getter ? 1 : 0) : opcode == (getter ? 0x6f : 0x28);
            if (!expectedCall) return false;
            if (getter) return RuntimeTypeMetadata.IsTypeHandleGetter(image, heaps, layout, row, definition);
            return definition ? RuntimeTypeMetadata.IsCoreGetTypeFromHandle(image, heaps, layout, row)
                : RuntimeTypeMetadata.IsGetTypeFromHandle(image, heaps, layout, row);
        }

        internal static int Resolve(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int coded, int context, int[] objects, int[] methodArguments, int[] target)
        {
            int status = RuntimeTypeTokens.ReadIdentity(bundle, image, layout, heaps, owner,
                coded, context, objects, methodArguments, target);
            if (status < 0) return status;
            int module = target[0], type = target[1];
            if (type >= RuntimeTypeTokens.PrimitiveVectorKey) return 0;
            if (type >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int closed = type - RuntimeTypeTokens.ClosedTypeKey;
                if (ObjectLayouts.ArrayElementKey(objects, closed) != 0) return 0;
                type = ObjectLayouts.TypeDef(objects, closed);
                target[2] = closed;
            }
            if (module == AssemblyBundle.ObjectOwner && type == 0)
            {
                status = RuntimeTypeTokens.FindCoreType(bundle, "Object", target);
                if (status < 0) return status;
                module = target[0];
                type = target[1];
            }
            if (module < 0 || type < 1) return -2;
            if (module != owner)
            {
                image = bundle.Get(module);
                layout = new int[4];
                heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            }
            target[1] = type;
            // An explicit trigger applies to both precise and BeforeFieldInit
            // types, with the same guard as implicit triggers.
            int initializer = TypeInitializers.Find(image, layout, heaps, type, new int[1]);
            if (initializer <= 0 || target[2] != 0) return initializer;

            // An open generic definition has no constructed static storage.
            int open = GenericParameters.Check(image, heaps, layout, type, new int[0], objects, new int[0]);
            return open == -2 ? 0 : open < 0 ? open : initializer;
        }

        internal static void Rewrite(int[] records, int token, int call, int initializer)
        {
            for (int source = token; source < call; source = records[source + 2] * Width)
                records[source] = Nop;
            records[call] = initializer < 0 ? Nop : Call;
            records[call + 1] = initializer;
            records[call + 3] = 0;
        }
    }
}
