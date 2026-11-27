using System;

namespace EventAccessorFixture
{
    public sealed class Holder
    {
        public int Seen;
        public void OnExit(object sender, EventArgs e) => Seen++;
    }

    public sealed class Publisher
    {
        public event EventHandler Changed;
        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    // Stage 181 (issue #256): AppDomain.ProcessExit's add/remove accessors are CoreLib's own
    // field-like event loops (Delegate.Combine/Remove + castclass + Interlocked.CompareExchange).
    // They run unchanged. Every kernel returns an int that the x86 CLR also returns.
    public static class Kernels
    {
        static int raised;
        static void OnExit(object sender, EventArgs e) { }
        static void OnChanged(object sender, EventArgs e) => raised++;

        public static int StaticHandler()
        {
            AppDomain.CurrentDomain.ProcessExit += OnExit;
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            return 1;
        }

        public static int InstanceHandler()
        {
            var holder = new Holder();
            AppDomain.CurrentDomain.ProcessExit += holder.OnExit;
            AppDomain.CurrentDomain.ProcessExit -= holder.OnExit;
            return 2 + holder.Seen; // the handler is never invoked before process exit
        }

        public static int RepeatedPairs()
        {
            int pairs = 0;
            for (int i = 0; i < 3; i++)
            {
                AppDomain.CurrentDomain.ProcessExit += OnExit;
                AppDomain.CurrentDomain.ProcessExit -= OnExit;
                pairs++;
            }
            return pairs;
        }

        // Removing a handler that was never added leaves the list unchanged, without an exception.
        public static int RemoveNeverAdded()
        {
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            return 4;
        }

        // Delegate.Combine and Remove accept null: adding or removing null is a no-op.
        public static int NullHandler()
        {
            AppDomain.CurrentDomain.ProcessExit += null;
            AppDomain.CurrentDomain.ProcessExit -= null;
            return 5;
        }

        // The CoreLib accessor and a library event in the same module, with the same handler kind.
        public static int MixedWithLibraryEvent()
        {
            raised = 0;
            var publisher = new Publisher();
            publisher.Changed += OnChanged;
            AppDomain.CurrentDomain.ProcessExit += OnChanged;
            publisher.Raise();
            AppDomain.CurrentDomain.ProcessExit -= OnChanged;
            publisher.Changed -= OnChanged;
            publisher.Raise();
            return raised;
        }
    }
}
