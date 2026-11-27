using System;
using System.Reflection;

namespace GenericKind
{
    public interface IKind<T> { }

    public sealed class Outer<T>
    {
        public sealed class Inner { }
    }

    public enum Marker { Zero }

    public static class Probe
    {
        public static int PlainTypes() =>
            !typeof(object).IsGenericType && !typeof(int).IsGenericType && !typeof(string).IsGenericType &&
            !typeof(Marker).IsGenericType && !typeof(void).IsGenericType ? 1 : 0;

        public static int OpenDefinitions() =>
            typeof(IKind<>).IsGenericType && typeof(Outer<>.Inner).IsGenericType ? 1 : 0;

        public static int ClosedTypes() =>
            typeof(IKind<int>).IsGenericType && typeof(Outer<int>.Inner).IsGenericType &&
            typeof(int?).IsGenericType ? 1 : 0;

        public static int ArraysOfGenericElements()
        {
            return !typeof(int[]).IsGenericType && !typeof(IKind<int>[]).IsGenericType &&
                !typeof(Type[]).IsGenericType && !new Outer<int>.Inner[1].GetType().IsGenericType ? 1 : 0;
        }

        private static bool Read(Type type) => type.IsGenericType;

        public static int DescriptorsThroughParameter()
        {
            bool open = Read(typeof(IKind<>));
            bool constructed = Read(typeof(IKind<>).MakeGenericType(typeof(uint)));
            return open && constructed && !Read(typeof(long)) ? 1 : 0;
        }

        private static bool MaybeRead(bool useType)
        {
            Type type = null;
            if (useType) type = typeof(IKind<int>);
            return type.IsGenericType;
        }

        public static int DescriptorNullReceiver()
        {
            if (!MaybeRead(true)) return 0;
            try
            {
                return MaybeRead(false) ? 0 : 0;
            }
            catch (NullReferenceException)
            {
                return 1;
            }
        }

        private sealed class AlwaysGeneric : TypeDelegator
        {
            public override bool IsGenericType => true;
        }

        private sealed class NeverGeneric : TypeDelegator
        {
            public override bool IsGenericType => false;
        }

        private static bool ReadNominal(Type type) => type.IsGenericType;

        public static int NominalVirtualOverrides() =>
            ReadNominal(new AlwaysGeneric()) && !ReadNominal(new NeverGeneric()) ? 1 : 0;
    }
}
