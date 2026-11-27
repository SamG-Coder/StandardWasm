namespace FunctionPointerFixture
{
    public unsafe struct Operation
    {
        public int Seed;
        public delegate*<int, int, int> Combine;
    }

    // Stage 150 (decision 0078): managed function pointers. ldftn of a static method yields
    // a native-size code address and calli calls through it with the call-site signature.
    public static unsafe class Kernels
    {
        private static int Add(int a, int b) => a + b;
        private static int Subtract(int a, int b) => a - b;
        private static int Multiply(int a, int b) => a * b;
        private static long Widen(int a, long b) => a * b;
        private static void Store(int* target, int value) => *target = value;

        private static delegate*<int, int, int> Pick(int which)
        {
            if (which == 0) return &Add;
            if (which == 1) return &Subtract;
            return &Multiply;
        }

        public static int Select(int which, int x, int y) => Pick(which)(x, y);

        // Pointers kept in an array and folded over a range.
        public static int Fold(int count)
        {
            var table = stackalloc delegate*<int, int, int>[3];
            table[0] = &Add;
            table[1] = &Subtract;
            table[2] = &Multiply;
            int total = 1;
            for (int i = 1; i <= count; i++) total = table[i % 3](total, i);
            return total;
        }

        // A pointer held in a struct field and passed as an argument.
        public static int Field(int which, int value)
        {
            var operation = new Operation { Seed = value, Combine = Pick(which) };
            return Apply(operation.Combine, operation.Seed, 3);
        }

        private static int Apply(delegate*<int, int, int> function, int a, int b) => function(a, b);

        // Other signatures: a wide result and a void callee writing through a pointer.
        public static long Wide(int a, int b)
        {
            delegate*<int, long, long> widen = &Widen;
            return widen(a, b * 1000000L);
        }

        public static int Void(int value)
        {
            int* target = stackalloc int[1];
            delegate*<int*, int, void> store = &Store;
            store(target, value);
            return *target;
        }

        // Calling a null function pointer is an access violation on the CLR, which ends the
        // process. Wasm raises an engine trap (call_indirect of table slot 0), not a managed
        // exception. The runner checks this case in Wasm only.
        public static int CallNull(int value)
        {
            delegate*<int, int, int> function = null;
            return function(value, value);
        }

        // Recursion through a pointer.
        private static int Factorial(int n) => n <= 1 ? 1 : n * ((delegate*<int, int>)&Factorial)(n - 1);
        public static int Recurse(int n) => Factorial(n);

        // Two loads of the same method give the same address on the CLR.
        public static int Same(int which, int other) => Pick(which) == Pick(other) ? 1 : 0;

        public static int IsNull(int which)
        {
            delegate*<int, int, int> function = which == 0 ? null : Pick(which);
            return function == null ? 1 : 0;
        }

        // Still diagnosed: unmanaged calling conventions are P/Invoke territory (#274).
        public static int RejectUnmanaged(delegate* unmanaged[Cdecl]<int, int> function, int value) => function(value);
    }
}
