extern alias ThirdParty;

namespace PlatformIdentityProbe
{
    public sealed class EnumReference : ThirdParty::System.Enum
    {
        public int value__;
    }

    public sealed class ValueReference : ThirdParty::System.ValueType
    {
        public int Value;
    }

    public sealed class EnumCarrier
    {
        public EnumReference Item;
    }

    public sealed class ValueCarrier
    {
        public ValueReference Item;
    }

    public static class ValueBaseProbe
    {
        public static int AliasEnum(int value)
        {
            var original = new EnumReference();
            EnumReference alias = original;
            alias.value__ = value;
            return original.value__;
        }

        public static int AliasValueType(int value)
        {
            var original = new ValueReference();
            ValueReference alias = original;
            alias.Value = value;
            return original.Value;
        }

        public static int EnumLayout(EnumCarrier carrier) => 17;

        public static int ValueLayout(ValueCarrier carrier) => 23;
    }
}
