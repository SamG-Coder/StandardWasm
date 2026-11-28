using System;

namespace DependentConstraints
{
    public interface IService { }
    public class Service : IService { }
    public sealed class DerivedService : Service { }
    public struct ValueService : IService
    {
        public int Value;
    }

    public static class ServiceFactory<TService>
    {
        public static int Bind<TImplementation>(int marker) where TImplementation : TService => marker;
    }

    public static class Probe
    {
        private static int Bind<TService, TImplementation>(int marker) where TImplementation : TService => marker;

        public static int InterfaceBound() => Bind<IService, DerivedService>(113);
        public static int BaseBound() => Bind<Service, DerivedService>(127);
        public static int ExactScalar() => Bind<int, int>(131);
        public static int AggregateBoxing() => Bind<IService, ValueService>(137);
        public static int PrimitiveBoxing() => Bind<IComparable, int>(139);
        public static int DeclaringTypeBound() => ServiceFactory<IService>.Bind<Service>(149);
    }
}
