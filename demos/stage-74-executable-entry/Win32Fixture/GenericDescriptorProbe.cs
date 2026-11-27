public sealed class DescriptorMarker<T>
{
}

public static class GenericDescriptorProbe
{
    public static int CheckGenericDescriptors(int mode)
    {
        var closed = typeof(DescriptorMarker<bool>);
        if (mode == 0) return closed == typeof(DescriptorMarker<bool>) ? 1 : 0;
        if (mode == 1) return closed != typeof(DescriptorMarker<string>) ? 1 : 0;
        return closed == new DescriptorMarker<bool>().GetType() ? 1 : 0;
    }
}
