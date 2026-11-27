using System;

public sealed class TypeCarrier
{
}

public static class TypeReturnProbe
{
    public static int CompareReturnedType(int mode)
    {
        var value = new TypeCarrier();
        Type received = ReadType(mode, value);
        return mode == 0 ? received == null ? 1 : 0 : received == typeof(TypeCarrier) ? 1 : 0;
    }

    private static Type ReadType(int mode, TypeCarrier value) =>
        mode == 0 ? null : value.GetType();
}
