using System.Runtime.CompilerServices;
using ManagedFinalizationExperiment;

internal static class SuppressionCases
{
    internal static object Verify()
    {
        string nullException = "none";
        try { FinalizableAllocations.Suppress(null); }
        catch (ArgumentNullException error) when (error.ParamName == "obj") { nullException = error.GetType().Name; }

        int initialCount = FinalizableAllocations.Finalized;
        int initialTotal = FinalizableAllocations.Total;
        ArrangeRegistered();
        Collect();
        int registered = FinalizableAllocations.Finalized - initialCount;
        int registeredTotal = FinalizableAllocations.Total - initialTotal;

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try
        {
            AllocateBlocker(entered, release);
            GC.Collect();
            if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new Exception("The CLR finalizer thread did not reach the blocker");
            // The finalizer thread is held while a forced collection queues this receiver.
            SuppressPending(AllocatePending());
        }
        finally
        {
            release.Set();
            GC.WaitForPendingFinalizers();
        }
        int pending = FinalizableAllocations.Finalized - initialCount - registered;
        ArrangeDuplicate();
        Collect();
        int duplicate = FinalizableAllocations.Finalized - initialCount - registered - pending;
        if (nullException == "none" || registered != 1 || registeredTotal != 37 || pending != 0 || duplicate != 1 ||
            FinalizableAllocations.Total - initialTotal != 80) throw new Exception("Actual CLR suppression results changed");
        return new { nullException, registered, pending, duplicate, total = FinalizableAllocations.Total - initialTotal };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ArrangeRegistered()
    {
        object suppressed = FinalizableAllocations.Allocate(1, 31);
        FinalizableAllocations.Suppress(suppressed);
        FinalizableAllocations.Suppress(suppressed);
        FinalizableAllocations.Suppress(FinalizableAllocations.Allocate(0, 0));
        GC.KeepAlive(FinalizableAllocations.Allocate(2, 37));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AllocatePending() => new(FinalizableAllocations.Allocate(1, 41), trackResurrection: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SuppressPending(WeakReference weak)
    {
        GC.Collect();
        FinalizableAllocations.Suppress(weak.Target ?? throw new Exception("The pending CLR receiver was lost"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ArrangeDuplicate()
    {
        object receiver = FinalizableAllocations.Allocate(1, 43);
        GC.ReRegisterForFinalize(receiver);
        FinalizableAllocations.Suppress(receiver);
        FinalizableAllocations.Suppress(receiver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateBlocker(ManualResetEventSlim entered, ManualResetEventSlim release) =>
        GC.KeepAlive(new BlockingFinalizer(entered, release));

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private sealed class BlockingFinalizer(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        ~BlockingFinalizer()
        {
            entered.Set();
            release.Wait();
        }
    }
}
