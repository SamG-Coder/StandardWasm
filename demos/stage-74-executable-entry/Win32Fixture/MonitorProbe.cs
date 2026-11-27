using System;
using System.Threading;

public static class MonitorProbe
{
    public static int LockRoundTrip(int scenario)
    {
        object target = new object();
        bool taken = scenario == 1;
        try
        {
            Monitor.Enter(scenario == 2 ? null : target, ref taken);
            if (!taken) return 0;
            lock (target)
            {
                if (scenario == 3) return 9 / (scenario - 3);
            }
            return 1;
        }
        catch (ArgumentNullException) { return taken ? 10 : 2; }
        catch (ArgumentException) { return taken ? 3 : 30; }
        catch (DivideByZeroException) { return taken ? 4 : 40; }
        finally
        {
            if (taken && scenario != 1) Monitor.Exit(target);
        }
    }

    public static int ReleaseDepth(int scenario)
    {
        object first = new object();
        object second = new object();
        Monitor.Enter(first);
        Monitor.Enter(first);
        Monitor.Exit(first);
        try
        {
            Monitor.Exit(scenario == 1 ? second : first);
            if (scenario == 2) Monitor.Exit(first);
            return 1;
        }
        catch (SynchronizationLockException)
        {
            if (scenario == 1) Monitor.Exit(first);
            return 2;
        }
    }
}
