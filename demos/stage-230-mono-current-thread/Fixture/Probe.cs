using System;
using System.Threading;

namespace MonoCurrentThread
{
    public static class Probe
    {
        public static int Identity()
        {
            Thread original = Thread.CurrentThread;
            return ReferenceEquals(original, Thread.CurrentThread) ? 1 : 0;
        }

        public static int ManagedId()
        {
            int id = Thread.CurrentThread.ManagedThreadId;
            return id > 0 && id == Environment.CurrentManagedThreadId ? 1 : 0;
        }

        public static int CurrentContexts()
        {
            Thread original = Thread.CurrentThread;
            SynchronizationContext previous = SynchronizationContext.Current;
            var next = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(next);
            bool current = ReferenceEquals(Thread.CurrentThread, original) &&
                ReferenceEquals(SynchronizationContext.Current, next);
            SynchronizationContext.SetSynchronizationContext(previous);
            return current && ReferenceEquals(SynchronizationContext.Current, previous) ? 1 : 0;
        }

        public static int PriorityRequiresNativeState()
        {
            return Thread.CurrentThread.Priority == ThreadPriority.Normal ? 1 : 0;
        }
    }
}
