public static class GenericValueProbe
{
    private readonly struct Slot<T>
    {
        private readonly T value;
        private readonly bool present;

        internal Slot(T item)
        {
            value = item;
            present = true;
        }

        internal bool IsPresent => present;
        internal T GetValueOrDefault() => value;
    }

    public static int ReadGenericValue(int state)
    {
        Slot<bool> slot = state < 0 ? default : new Slot<bool>(state != 0);
        return slot.IsPresent ? (slot.GetValueOrDefault() ? 1 : 0) : 2;
    }
}
