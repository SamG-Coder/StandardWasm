using System;
using InterfaceRelationBridge;
using InterfaceRelationContract;

namespace RuntimeInterfaceAssignableTo
{
    public interface IInvariant<T> { }
    public interface IDerived<T> : IInvariant<T> { }
    public interface ICovariant<out T> { }

    public class Parent : IDerived<Child> { }
    public sealed class Child : Parent { }
    public sealed class Plain { }
    public sealed class Signed : IInvariant<int> { }
    public sealed class Variant : ICovariant<Child> { }
    public sealed class Partial : IBridge<Partial> { }
    public enum Flag : ulong { High = 1UL << 63 }

    public struct State : IInvariant<State>
    {
        public int Number;
    }

    public static class Probe
    {
        private static bool CheckSelf(Type source) => source.IsAssignableTo(
            typeof(IInvariant<>).MakeGenericType(source));

        public static int Transitive()
        {
            return CheckSelf(new Child().GetType()) && !CheckSelf(new Plain().GetType()) ? 1 : 0;
        }

        public static int ExactClosedArguments()
        {
            bool signed = typeof(Signed).IsAssignableTo(typeof(IInvariant<>).MakeGenericType(typeof(int)));
            bool unsigned = typeof(Signed).IsAssignableTo(typeof(IInvariant<>).MakeGenericType(typeof(uint)));
            return signed && !unsigned ? 1 : 0;
        }

        public static int ScalarWithNominalLayout()
        {
            int value = 37;
            if (value.CompareTo(43) >= 0) return 0;
            bool signed = typeof(int).IsAssignableTo(typeof(IEquatable<>).MakeGenericType(typeof(int)));
            return signed && typeof(IEquatable<>).MakeGenericType(typeof(int)) == typeof(IEquatable<int>) ? 1 : 0;
        }

        public static int StructInterface()
        {
            object boxed = new State { Number = 17 };
            return CheckSelf(boxed.GetType()) ? ((State)boxed).Number : 0;
        }

        public static int EnumWithoutInterface()
        {
            bool found = typeof(Flag).IsAssignableTo(typeof(IInvariant<>).MakeGenericType(typeof(Flag)));
            return !found && typeof(IInvariant<>).MakeGenericType(typeof(Flag)) == typeof(IInvariant<Flag>) ? 1 : 0;
        }

        public static int NullTarget() => typeof(Child).IsAssignableTo(null) ? 0 : 1;

        public static int NullReceiver()
        {
            try
            {
                return ((Type)null).IsAssignableTo(typeof(IInvariant<>).MakeGenericType(typeof(int))) ? 0 : 0;
            }
            catch (NullReferenceException)
            {
                return 1;
            }
        }

        public static int NullReceiverAndTarget()
        {
            try
            {
                return ((Type)null).IsAssignableTo(null) ? 0 : 0;
            }
            catch (NullReferenceException)
            {
                return 1;
            }
        }

        public static int ProvenInterfaceWithMissingEdge() => typeof(Partial).IsAssignableTo(
            typeof(IExternal<>).MakeGenericType(typeof(Partial))) ? 1 : 0;

        public static int IncompleteInterfaceClosure() => typeof(Partial).IsAssignableTo(
            typeof(IExternal<>).MakeGenericType(typeof(object))) ? 1 : 0;

        public static int UnsupportedVariance() => typeof(Variant).IsAssignableTo(
            typeof(ICovariant<>).MakeGenericType(typeof(Parent))) ? 1 : 0;
    }
}
