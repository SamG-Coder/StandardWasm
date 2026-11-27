using System;
using System.Threading;

namespace CoreLibMonitors
{
    public static class Probe
    {
        public static int Nested()
        {
            object gate = new object();
            bool entered = false;
            Monitor.Enter(gate, ref entered);
            if (!entered) return 0;
            Monitor.Enter(gate);
            Monitor.Exit(gate);
            Monitor.Exit(gate);
            try { Monitor.Exit(gate); }
            catch (SynchronizationLockException) { return 479; }
            return 0;
        }

        public static int AlreadyTrue()
        {
            bool entered = true;
            try { Monitor.Enter(null, ref entered); }
            catch (ArgumentException) { return entered ? 487 : 0; }
            return 0;
        }

        public static int NullGate()
        {
            bool entered = false;
            try { Monitor.Enter(null, ref entered); }
            catch (ArgumentNullException) { return entered ? 0 : 491; }
            return 0;
        }

        public static int OriginalSetting()
        {
            return AppContext.GetData("StandardWasm.Stage180.Unset") == null ? 499 : 0;
        }

        public static int SharedLockTaken()
        {
            object gate = new object();
            Initialize(out bool entered);
            try
            {
                Monitor.Enter(gate, ref entered);
                return Read(ref entered) ? 577 : 0;
            }
            finally
            {
                if (entered) Monitor.Exit(gate);
            }
        }

        private static void Initialize(out bool entered) => entered = false;

        private static bool Read(ref bool entered) => entered;
    }
}
