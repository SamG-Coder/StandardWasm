using System;

namespace BooleanClassByrefs
{
    internal class Base
    {
        internal bool First;
        internal bool Second;
        internal int Number;
        internal byte Byte;
    }

    internal sealed class Derived : Base
    {
        internal int Extra;
    }

    public static class Probe
    {
        private static bool Swap(ref bool target, bool replacement)
        {
            bool previous = target;
            target = replacement;
            return previous;
        }

        private static void SetNumber(ref int target, int replacement) => target = replacement;

        public static int ClassAlias()
        {
            var value = new Derived();
            value.Extra = 7;
            var other = new Base();
            ref bool first = ref value.First;
            ref bool second = ref value.Second;
            Swap(ref first, true);
            Swap(ref other.First, true);
            bool seenFromSecondAlias = Swap(ref value.First, false);
            Swap(ref second, true);
            SetNumber(ref value.Number, 101);
            return seenFromSecondAlias && !value.First && value.Second && other.First && !other.Second &&
                value.Extra == 7 ? value.Number : 0;
        }

        public static int ClassAndLocal()
        {
            var holder = new Base();
            bool local = false;
            Swap(ref holder.First, true);
            Swap(ref local, true);
            return Swap(ref holder.First, false) && Swap(ref local, false) && !holder.First && !local ? 103 : 0;
        }

        public static int NullReceiver()
        {
            try
            {
                Base missing = null;
                Swap(ref missing.First, true);
                return 0;
            }
            catch (NullReferenceException)
            {
                return 107;
            }
        }

        private static void SetByte(ref byte value) => value = 113;

        public static int PackedByteBoundary()
        {
            var holder = new Base();
            SetByte(ref holder.Byte);
            return holder.Byte;
        }
    }
}
