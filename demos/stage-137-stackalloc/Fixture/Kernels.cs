namespace System.Runtime.CompilerServices
{
    // netstandard2.0 has no SkipLocalsInitAttribute; C# recognizes it by name.
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class SkipLocalsInitAttribute : Attribute { }
}

namespace StackAllocFixture
{
    // Stage 137 (decision 0065): stackalloc into primitive pointers on the shadow stack
    // in memory 0. Every kernel is stateless, so the CLR and a fresh Wasm instance agree
    // call by call.
    public static unsafe class Kernels
    {
        public static int Bytes(int count)
        {
            byte* buffer = stackalloc byte[count];
            for (int i = 0; i < count; i++) buffer[i] = unchecked((byte)(i * 3 + 1));
            int sum = 0;
            for (int i = 0; i < count; i++) sum += buffer[i];
            return sum;
        }

        public static int Ints(int count)
        {
            int* values = stackalloc int[count];
            int zeros = 0;
            for (int i = 0; i < count; i++) if (values[i] == 0) zeros++;
            for (int i = 0; i < count; i++) values[i] = i * i;
            long total = 0;
            for (int i = 0; i < count; i++) total += values[i];
            return unchecked((int)total + zeros * 1000000);
        }

        private static int Inner(int n)
        {
            int* local = stackalloc int[4];
            local[0] = n; local[3] = n * 2;
            return local[0] + local[3];
        }

        public static int Nested(int n)
        {
            int* outer = stackalloc int[2];
            outer[0] = 5; outer[1] = 7;
            int a = Inner(n), b = Inner(n + 1);
            return outer[0] * 100 + outer[1] + a * 10000 + b;
        }

        private static int Depth(int n)
        {
            byte* frame = stackalloc byte[256];
            frame[0] = (byte)n; frame[255] = (byte)(n + 1);
            if (n == 0) return frame[0] + frame[255];
            return Depth(n - 1) + frame[0] + frame[255];
        }

        public static int Recursion(int n) => Depth(n);

        private static int zero;

        private static int ThrowsAfterAllocation(int fill)
        {
            byte* block = stackalloc byte[65536];
            block[0] = (byte)fill; block[65535] = (byte)fill;
            return block[0] / zero;
        }

        // 100 x 64 KiB frames that throw: without release on the exceptional exit the
        // 1 MiB shadow stack is exhausted after 16 rounds.
        public static int AfterException(int rounds)
        {
            int caught = 0;
            for (int i = 0; i < rounds; i++)
            {
                try { ThrowsAfterAllocation(i); }
                catch (System.DivideByZeroException) { caught++; }
            }
            return caught * 10000 + Bytes(37);
        }

        // Repeated top-level allocations must not exhaust the region: each call frees on return.
        // [SkipLocalsInit] leaves stackalloc contents unspecified; this kernel writes
        // before reading, so the result is defined on both runtimes.
        [System.Runtime.CompilerServices.SkipLocalsInit]
        public static int Uninitialized(int count)
        {
            long* values = stackalloc long[count];
            for (int i = 0; i < count; i++) values[i] = (long)i << 33;
            long sum = 0;
            for (int i = 0; i < count; i++) sum += values[i] >> 33;
            return (int)sum;
        }

        // The element count times the element size overflows the 32-bit native size:
        // mul.ovf.un raises OverflowException before anything is allocated.
        public static int OversizedCount()
        {
            try
            {
                int* values = stackalloc int[0x40000000];
                return values[0];
            }
            catch (System.OverflowException) { return -11; }
        }

        // Wasm only: 2 MiB exceeds the 1 MiB shadow stack and traps. The CLR's
        // StackOverflowException would end the process.
        public static int Exhaust()
        {
            byte* block = stackalloc byte[2 << 20];
            return block[0];
        }

        // The allocation is never reached: no memory is reserved.
        public static int Untaken(int flag)
        {
            if (flag != 0)
            {
                byte* block = stackalloc byte[16];
                block[0] = 1;
                return block[0];
            }
            return 79;
        }

        // The outer frame enters before the region exists; the callee reserves it. The outer
        // frame's later allocation and its release must reuse that one reservation.
        public static int OuterAfterInner(int n)
        {
            int inner = Inner(n);
            byte* block = stackalloc byte[64];
            block[63] = (byte)n;
            return inner * 10 + block[63];
        }

        // CoreCLR returns null for a zero-sized stackalloc.
        public static int ZeroIsNull(int count)
        {
            byte* block = stackalloc byte[count];
            return block == null ? 1 : 0;
        }

        // Wasm only: 600 KiB stays allocated when the nested Exhaust traps, so the frame
        // never releases it. The next host call must still find an empty shadow stack.
        public static int ExhaustAfterPartial()
        {
            byte* block = stackalloc byte[600 << 10];
            block[0] = 1;
            return Exhaust() + block[0];
        }

        // 600 KiB fits once but not twice in the 1 MiB shadow stack.
        public static int Large()
        {
            byte* block = stackalloc byte[600 << 10];
            block[(600 << 10) - 1] = 9;
            return block[(600 << 10) - 1] + block[0];
        }

        public static int Repeated(int rounds)
        {
            int total = 0;
            for (int i = 0; i < rounds; i++) total += Bytes(4096) & 0xff;
            return total;
        }
    }
}
