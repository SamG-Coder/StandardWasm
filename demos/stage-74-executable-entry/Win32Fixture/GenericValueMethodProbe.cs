public struct GenericMethodSlot<T>
{
    private T current;

    public void StoreGeneric<TMarker>(T value, TMarker marker) => current = value;

    public T ReadGeneric<TMarker>(TMarker marker) => current;
}

public sealed class GenericDefaultReference
{
}

public static class GenericValueMethodProbe
{
    public static int VerifyGenericValueMethod(int mode)
    {
        GenericMethodSlot<bool> slot = default;
        slot.StoreGeneric(mode == 1, 12);
        if (mode == 2) slot.StoreGeneric(true, false);
        return slot.ReadGeneric(false) ? 1 : 0;
    }

    public static int VerifyGenericDefaultReference(int mode)
    {
        var value = new GenericDefaultReference();
        var result = SelectReference(mode, value);
        return result == null ? 0 : result == value ? 1 : 2;
    }

    private static T SelectReference<T>(int mode, T value)
    {
        T selected = default;
        if (mode != 0) selected = value;
        return selected;
    }
}
