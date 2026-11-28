extern alias lookalike;

using System;
using System.Reflection;
using FakeEnum = lookalike::System.Enum;

namespace RuntimeTypeEnumKind
{
    public enum ByteValue : byte { First }
    public enum Signed : long { Low = long.MinValue }
    public enum Unsigned : ulong { High = 1UL << 63 }

    public sealed class Nested<T>
    {
        public enum Enum : ushort { First }
    }

    public class Base<T> { }
    public sealed class WithGenericBase<T> : Base<T> { }
    public sealed class DerivedFromLookalike : FakeEnum { }

    public static class Probe
    {
        public static int OrdinaryEnums() =>
            typeof(ByteValue).IsEnum && typeof(Signed).IsEnum && typeof(Unsigned).IsEnum ? 1 : 0;

        public static int PrimitiveAndBaseTypes() =>
            !typeof(byte).IsEnum && !typeof(long).IsEnum && !typeof(ulong).IsEnum &&
            !typeof(System.Enum).IsEnum && !typeof(ValueType).IsEnum && !typeof(object).IsEnum ? 1 : 0;

        public static int LookalikeIdentity() =>
            !typeof(FakeEnum).IsEnum && !typeof(DerivedFromLookalike).IsEnum &&
            typeof(Nested<>.Enum).IsEnum ? 1 : 0;

        public static int ArraysAndGenericBases() =>
            !typeof(Signed[]).IsEnum && !typeof(byte[]).IsEnum &&
            !typeof(WithGenericBase<>).IsEnum && !typeof(WithGenericBase<int>).IsEnum ? 1 : 0;

        private static bool Read(Type type) => type.IsEnum;

        public static int RuntimeDescriptorParameters() =>
            Read(typeof(Signed)) && !Read(new WithGenericBase<int>().GetType()) && !Read(typeof(long)) ? 1 : 0;

        private static bool MaybeRead(bool useType)
        {
            Type type = null;
            if (useType) type = typeof(Unsigned);
            return type.IsEnum;
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

        private sealed class AlwaysEnum : TypeDelegator
        {
            public override bool IsEnum => true;
        }

        private sealed class NeverEnum : TypeDelegator
        {
            public override bool IsEnum => false;
        }

        private static bool ReadNominal(Type type) => type.IsEnum;

        public static int NominalVirtualOverrides() =>
            ReadNominal(new AlwaysEnum()) && !ReadNominal(new NeverEnum()) ? 1 : 0;
    }
}
