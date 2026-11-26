using PeLibrary;

namespace PortableCompiler
{
    internal sealed class MethodInstantiations
    {
        private MethodArgumentSet[] arguments;

        internal int[] Arguments(int function)
        {
            MethodArgumentSet set = arguments == null ? null : arguments[function];
            return set == null ? null : set.Types;
        }

        internal int Find(int[] owners, int[] rows, int[] contexts, int count,
            int owner, int row, int context, int[] types)
        {
            if (arguments == null) return -1;
            for (int function = 0; function < count; function++)
            {
                int[] candidate = Arguments(function);
                if (candidate == null || candidate.Length != types.Length ||
                    owners[function] != owner || rows[function] != row || contexts[function] != context)
                    continue;
                int i = 0;
                while (i < types.Length && candidate[i] == types[i]) i++;
                if (i == types.Length) return function;
            }
            return -1;
        }

        internal void Record(int function, int capacity, int[] types)
        {
            if (arguments == null) arguments = new MethodArgumentSet[capacity];
            arguments[function] = new MethodArgumentSet(types);
        }

        // Keep exact CLI type keys until a parameter is written into a Wasm slot.
        internal static int ReadArguments(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int context, int[] objects, int[] callerArguments, int[] spec, int[] types)
        {
            var next = new[] { spec[MethodSpecifications.Arguments] };
            int end = spec[MethodSpecifications.End];
            for (int i = 0; i < types.Length; i++)
            {
                int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner,
                    next, end, context, objects, 0, methodArguments: callerArguments);
                if (type < 0) return type;
                types[i] = type;
            }
            return next[0] == end ? 0 : -1;
        }
    }

    internal sealed class MethodArgumentSet
    {
        internal readonly int[] Types;

        internal MethodArgumentSet(int[] types)
        {
            Types = types;
        }
    }
}
