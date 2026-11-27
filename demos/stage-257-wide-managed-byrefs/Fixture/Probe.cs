using System;

namespace WideManagedByrefs
{
    public static class Probe
    {
        [Flags]
        private enum Flags : ulong
        {
            Initial = 1UL << 32,
            High = 1UL << 63
        }

        private sealed class Owner
        {
            internal Flags Flags;
            internal Payload Target;
            internal int Count;
            internal long Signed;
        }

        private sealed class Payload
        {
            internal int Number;
        }

        private struct Numbers
        {
            internal long Signed;
            internal ulong Unsigned;
        }

        public static int MixedClassFields()
        {
            var target = new Payload { Number = 7 };
            var owner = new Owner { Flags = Flags.Initial, Count = 3 };
            var other = new Owner { Flags = Flags.High, Count = 19 };
            bool wasInitial = Exchange(ref owner.Flags, ref owner.Target, ref owner.Count, target);
            owner.Flags |= Flags.Initial;
            bool sawLiveStore = SeesBoth(ref owner.Flags);
            return (wasInitial ? 1 : 0) | (sawLiveStore ? 2 : 0) |
                (owner.Target == target && owner.Target.Number == 7 ? 4 : 0) |
                (owner.Count == 8 ? 8 : 0) |
                (other.Flags == Flags.High && other.Count == 19 ? 16 : 0);
        }

        private static bool Exchange(ref Flags flags, ref Payload target, ref int count, Payload replacement)
        {
            bool initial = flags == Flags.Initial;
            target = replacement;
            SetHigh(ref flags);
            count += 5;
            return initial;
        }

        private static void SetHigh(ref Flags flags) => flags = Flags.High;

        private static bool SeesBoth(ref Flags flags) => flags == (Flags.Initial | Flags.High);

        public static int LiveSignedClassField()
        {
            var owner = new Owner { Signed = long.MinValue };
            ref long signed = ref owner.Signed;
            owner.Signed += 43;
            bool fromOwner = signed == long.MinValue + 43;
            signed = long.MaxValue;
            return (fromOwner ? 1 : 0) | (owner.Signed == long.MaxValue ? 2 : 0);
        }

        public static int LocalValues()
        {
            long signed = long.MinValue;
            ulong unsigned = 1UL << 63;
            ChangeLocals(ref signed, ref unsigned);
            signed -= 1;
            ChangeLocals(ref signed, ref unsigned);
            return (signed == long.MinValue + 1 ? 1 : 0) |
                (unsigned == (1UL << 63) + 10 ? 2 : 0);
        }

        private static void ChangeLocals(ref long signed, ref ulong unsigned)
        {
            signed++;
            unsigned += 5;
        }

        public static int LiveValueFields()
        {
            var numbers = new Numbers { Signed = long.MinValue, Unsigned = 1UL << 63 };
            ref ulong alias = ref numbers.Unsigned;
            numbers.Unsigned += 13;
            bool seesWrite = alias == (1UL << 63) + 13;
            ChangeLocals(ref numbers.Signed, ref alias);
            return (seesWrite ? 1 : 0) |
                (numbers.Signed == long.MinValue + 1 ? 2 : 0) |
                (numbers.Unsigned == (1UL << 63) + 18 ? 4 : 0);
        }

        public static int ClosedGenericLoadStore()
        {
            var owner = new Owner { Flags = Flags.Initial };
            Flags value = Flags.High;
            Copy(ref owner.Flags, ref value);
            bool copiedToOwner = owner.Flags == Flags.High;
            Clear(ref owner.Flags);
            Copy(ref value, ref owner.Flags);
            return (copiedToOwner ? 1 : 0) | (owner.Flags == 0 ? 2 : 0) | (value == 0 ? 4 : 0);
        }

        private static void Copy<T>(ref T destination, ref T source) => destination = source;

        private static void Clear<T>(ref T destination) => destination = default(T);

        public static int NullOwner()
        {
            Owner owner = null;
            try
            {
                SetHigh(ref owner.Flags);
                return 0;
            }
            catch (NullReferenceException)
            {
                return 1;
            }
        }
    }
}
