using System;

public interface IFinalWriter<T> { bool Take(out T output); }

public class FinalWriter<T> : IFinalWriter<T>
{
    public bool Take(out T output)
    {
        output = default;
        return true;
    }
}

public static class FinalWriterProbe
{
    public static int InvokeFinalWriter(int scenario)
    {
        FinalWriter<bool> writer = scenario == 0 ? new FinalWriter<bool>() : null;
        try { return writer.Take(out bool value) && !value ? 1 : 0; }
        catch (NullReferenceException) { return 2; }
    }
}
