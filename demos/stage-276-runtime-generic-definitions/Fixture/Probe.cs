using System;
using System.Reflection;

namespace GenericDefinitions
{
    public interface IKind<T> { }

    public sealed class Outer<T>
    {
        public sealed class Nested<TValue> { }
        public sealed class Captured { }
    }

    public static class Probe
    {
        public static int OpenIdentity() =>
            ReferenceEquals(typeof(IKind<>), typeof(IKind<>).GetGenericTypeDefinition()) ? 1 : 0;

        public static int ClosedAndNested() =>
            typeof(IKind<int>).GetGenericTypeDefinition() == typeof(IKind<>) &&
            typeof(Outer<int>.Nested<string>).GetGenericTypeDefinition() == typeof(Outer<>.Nested<>) &&
            typeof(Outer<uint>.Captured).GetGenericTypeDefinition() == typeof(Outer<>.Captured) ? 1 : 0;

        public static int FactoryDefinition() =>
            typeof(IKind<>).MakeGenericType(typeof(uint)).GetGenericTypeDefinition() == typeof(IKind<>) ? 1 : 0;

        public static int RepeatedDefinition()
        {
            Type definition = typeof(Outer<int>.Nested<string>).GetGenericTypeDefinition();
            return ReferenceEquals(definition, definition.GetGenericTypeDefinition()) && definition.IsGenericType ? 1 : 0;
        }

        public static int NongenericType()
        {
            try
            {
                typeof(int).GetGenericTypeDefinition();
                return 0;
            }
            catch (InvalidOperationException error)
            {
                return error.GetType() == typeof(InvalidOperationException) &&
                    error.HResult == unchecked((int)0x80131509) ? 1 : 0;
            }
        }

        public static int ArrayType()
        {
            InvalidOperationException first;
            try
            {
                typeof(IKind<int>[]).GetGenericTypeDefinition();
                return 0;
            }
            catch (InvalidOperationException error)
            {
                first = error;
            }
            try
            {
                typeof(int[]).GetGenericTypeDefinition();
                return 0;
            }
            catch (InvalidOperationException error)
            {
                return !ReferenceEquals(first, error) && first.HResult == error.HResult ? 1 : 0;
            }
        }

        public static int NullReceiver()
        {
            try
            {
                ((Type)null).GetGenericTypeDefinition();
                return 0;
            }
            catch (NullReferenceException)
            {
                return 1;
            }
        }

        private sealed class ReturnsSelf : TypeDelegator
        {
            public override Type GetGenericTypeDefinition() => this;
        }

        private static Type ReadNominal(Type type) => type.GetGenericTypeDefinition();

        public static int NominalOverride()
        {
            Type nominal = new ReturnsSelf();
            return ReferenceEquals(ReadNominal(nominal), nominal) ? 1 : 0;
        }
    }
}
