using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReceiverDemandFixture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Counter
    {
        public int Value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Add(int change)
        {
            Value += change;
            return Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public T AddAndReturn<T>(int change, T value)
        {
            Value += change;
            return value;
        }
    }

    public static unsafe class Probe
    {
        public static int NativeOnly()
        {
            Counter* pointer = stackalloc Counter[1];
            pointer->Value = 17;
            int result = pointer->Add(7);
            return result * 100 + pointer->Value;
        }

        public static int Mixed()
        {
            Counter* pointer = stackalloc Counter[1];
            pointer->Value = 17;
            int result = pointer->Add(7);
            return result * 1000 + TraceCaller();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int TraceCaller()
        {
            var array = new Counter[1];
            return array[0].Add(13) + array[0].Value;
        }

        public static int NativeGenerics()
        {
            Counter* pointer = stackalloc Counter[1];
            pointer->Value = 23;
            int first = pointer->AddAndReturn(17, 19);
            long second = pointer->AddAndReturn(31, 72057594037927937L);
            return pointer->Value * 100 + first + (second == 72057594037927937L ? 1 : 0);
        }
    }
}
