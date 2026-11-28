public static class CurrentThreadTests
{
    public static int Identity()
    {
        Thread thread = Thread.CurrentThread;
        return thread == Thread.CurrentThread ? 1 : 0;
    }

    public static int ManagedId()
    {
        Thread thread = Thread.CurrentThread;
        int id = thread.ManagedThreadId;
        return id > 0 && id == Thread.CurrentThread.ManagedThreadId ? 1 : 0;
    }

    public static int Context()
    {
        Thread thread = Thread.CurrentThread;
        SynchronizationContext previous = SynchronizationContext.Current;
        var context = new SynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        bool same = thread == Thread.CurrentThread && SynchronizationContext.Current == context;
        SynchronizationContext.SetSynchronizationContext(previous);
        return same && SynchronizationContext.Current == previous ? 1 : 0;
    }

    public static int Priority()
    {
        return (int)Thread.CurrentThread.Priority;
    }

    public static object Reference() => Thread.CurrentThread;

    public static int NativeState() => (int)Thread.CurrentThread.ThreadState;
}
