namespace LocalStaticInterface.Contracts;

public interface IExternalLocation
{
    static abstract ref readonly int Value { get; }
}

public static class ExternalLocation
{
    public static ref readonly int Address<T>() where T : IExternalLocation => ref T.Value;
}
