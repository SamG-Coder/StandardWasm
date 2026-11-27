using System;
using System.Threading;

namespace VolatileCompareExchange
{
    public sealed class Slot
    {
        public volatile int Value;
    }

    public static class Probe
    {
        private static int shared;

        // Interlocked supplies ordering for the operation on this field's address.
#pragma warning disable CS0420
        public static int SuccessAndMismatch()
        {
            var slot = new Slot();
            slot.Value = -7;
            int first = Interlocked.CompareExchange(ref slot.Value, 21, -7);
            int second = Interlocked.CompareExchange(ref slot.Value, 49, -7);
            return first == -7 && second == 21 && slot.Value == 21 ? 901 : 0;
        }

        public static int SeparateSlots()
        {
            var first = new Slot();
            var second = new Slot();
            Interlocked.CompareExchange(ref first.Value, int.MinValue, 0);
            Interlocked.CompareExchange(ref second.Value, int.MaxValue, 0);
            int previous = Interlocked.CompareExchange(ref first.Value, 0, int.MaxValue);
            return previous == int.MinValue && first.Value == int.MinValue && second.Value == int.MaxValue ? 907 : 0;
        }

        public static int NullReceiver()
        {
            Slot slot = null;
            try
            {
                return Interlocked.CompareExchange(ref slot.Value, 73, 0);
            }
            catch (NullReferenceException) { return 911; }
        }
#pragma warning restore CS0420

        public static int StaticAndArray()
        {
            shared = 27;
            int[] values = { 13, 15 };
            int original = Interlocked.CompareExchange(ref shared, 44, 27);
            int mismatch = Interlocked.CompareExchange(ref values[1], 48, 14);
            int updated = Interlocked.CompareExchange(ref values[1], 52, 15);
            return original == 27 && shared == 44 && mismatch == 15 &&
                updated == 15 && values[0] == 13 && values[1] == 52 ? 919 : 0;
        }

        public static int ExistingExchange()
        {
            var slot = new Slot();
#pragma warning disable CS0420
            int original = Interlocked.Exchange(ref slot.Value, 83);
#pragma warning restore CS0420
            return original == 0 && slot.Value == 83 ? 929 : 0;
        }
    }
}
