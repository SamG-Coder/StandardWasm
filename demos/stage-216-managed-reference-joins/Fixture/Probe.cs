using System.Runtime.CompilerServices;

namespace ManagedReferenceJoins
{
    public interface IValue { int Read(); }

    public class Base
    {
        public readonly int Value;

        public Base(int value) => Value = value;
    }

    public sealed class Concrete : Base, IValue
    {
        public Concrete(int value) : base(value) { }

        public int Read() => Value;
    }

    public sealed class Unrelated
    {
        public readonly int Value;

        public Unrelated(int value) => Value = value;
    }

    public static class Probe
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int InterfaceAfterPrefix(int prefix, IValue value) =>
            prefix + (value == null ? -11 : value.Read());

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadBase(Base value) => value.Value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadObject(object value) => value != null ? 1 : 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IValue AsInterface(Concrete value) => value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Base AsBase(Concrete value) => value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int InterfaceFirst(bool takeFirst, IValue first, Concrete second) =>
            InterfaceAfterPrefix(100, takeFirst ? first : second);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ClassFirst(bool takeFirst, Concrete first, IValue second) =>
            InterfaceAfterPrefix(200, takeFirst ? first : second);

        public static int InterfaceThenClass()
        {
            var first = new Concrete(3);
            var second = new Concrete(5);
            IValue alreadyInterface = AsInterface(first);
            return InterfaceFirst(true, alreadyInterface, second) * 1000 +
                InterfaceFirst(false, alreadyInterface, second);
        }

        public static int ClassThenInterface()
        {
            var first = new Concrete(7);
            var second = new Concrete(9);
            IValue alreadyInterface = AsInterface(second);
            return ClassFirst(true, first, alreadyInterface) * 1000 +
                ClassFirst(false, first, alreadyInterface);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CoalesceInterface(IValue supplied, Concrete fallback) =>
            InterfaceAfterPrefix(300, supplied ?? fallback);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CoalesceClass(Concrete supplied, IValue fallback) =>
            InterfaceAfterPrefix(400, supplied ?? fallback);

        public static int CoalescedReferences()
        {
            var direct = new Concrete(31);
            IValue converted = AsInterface(new Concrete(37));
            return CoalesceInterface(null, direct) + CoalesceInterface(converted, direct) * 1000 +
                CoalesceClass(null, converted) + CoalesceClass(direct, converted) * 1000;
        }

        public static int ClassAndBase()
        {
            var child = new Concrete(13);
            Base parent = AsBase(new Concrete(17));
            int sum = 0;
            for (int i = 0; i < 4; i++)
            {
                sum += ReadBase(i % 2 == 0 ? child : parent);
                sum += ReadBase(i % 2 == 0 ? parent : child);
            }
            return sum;
        }

        public static int NullAndLoop()
        {
            var concrete = new Concrete(19);
            IValue throughInterface = AsInterface(new Concrete(23));
            int sum = 0;
            for (int i = 0; i < 5; i++)
            {
                IValue possiblyNull = i == 2 ? null : throughInterface;
                sum += i % 2 == 0 ? InterfaceFirst(false, possiblyNull, concrete) :
                    ClassFirst(false, concrete, possiblyNull);
                sum += InterfaceFirst(true, possiblyNull, concrete);
            }
            return sum;
        }

        public static int SwitchAndLoop()
        {
            var concrete = new Concrete(29);
            IValue throughInterface = AsInterface(new Concrete(31));
            int sum = 0;
            for (int i = 0; i < 6; i++)
            {
                switch (i)
                {
                    case 0: sum += ClassFirst(true, concrete, throughInterface); break;
                    case 1: sum += InterfaceFirst(true, throughInterface, concrete); break;
                    case 2: sum -= ClassFirst(false, concrete, throughInterface); break;
                    case 3: sum += InterfaceFirst(false, throughInterface, concrete); break;
                    default: sum += concrete.Read(); break;
                }
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int UnrelatedJoin(bool choose, Concrete first, Unrelated second) =>
            ReadObject(choose ? first : (object)second);

        public static int UnrelatedDiagnostic() =>
            UnrelatedJoin(true, new Concrete(37), new Unrelated(41));
    }
}
