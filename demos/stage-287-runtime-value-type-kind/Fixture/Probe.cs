using System;
using System.Reflection;

namespace RuntimeValueTypeKind
{
    public enum Flag : long { First }
    public struct Payload<T> { public int Number; }
    public sealed class Carrier<T> { }

    public static class Probe
    {
        public static int PrimitiveAndStruct() =>
            typeof(int).IsValueType && typeof(decimal).IsValueType && typeof(Payload<int>).IsValueType ? 101 : 0;

        public static int OpenAndClosedGeneric() =>
            typeof(Payload<>).IsValueType && typeof(Nullable<>).IsValueType &&
            typeof(int?).IsValueType && !typeof(Carrier<>).IsValueType && !typeof(Carrier<int>).IsValueType ? 103 : 0;

        public static int ActualEnumAndBaseTypes() =>
            typeof(Flag).IsValueType && !typeof(Enum).IsValueType && !typeof(ValueType).IsValueType ? 107 : 0;

        public static int VectorsAndReferences() =>
            !typeof(byte[]).IsValueType && !typeof(Flag[]).IsValueType &&
            !typeof(Payload<int>[]).IsValueType && !typeof(object).IsValueType && !typeof(string).IsValueType ? 109 : 0;

        private static bool ReadDescriptor(Type type) => type.IsValueType;

        public static int DescriptorParameters() =>
            ReadDescriptor(typeof(Flag)) && !ReadDescriptor(new Carrier<int>().GetType()) ? 113 : 0;

        public static int TypedNull()
        {
            try { return ReadDescriptor(null) ? 0 : 0; }
            catch (NullReferenceException) { return 127; }
        }

        private sealed class AlwaysValue : TypeDelegator
        {
            protected override bool IsValueTypeImpl() => true;
        }

        private sealed class NeverValue : TypeDelegator
        {
            protected override bool IsValueTypeImpl() => false;
        }

        private static bool ReadNominal(Type type) => type.IsValueType;

        public static int NominalProtectedOverrides() =>
            ReadNominal(new AlwaysValue()) && !ReadNominal(new NeverValue()) ? 131 : 0;
    }
}
