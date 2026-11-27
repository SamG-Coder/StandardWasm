namespace ManagedFinalizationExperiment
{
    public static class FinalizableAllocations
    {
        private static int finalized;
        private static int total;
        private static int overrides;
        private static object resurrected;

        public static object Allocate(int kind, int value)
        {
            if (kind == 0) return new NewSlot();
            if (kind == 1) return new Registered(value);
            if (kind == 2) return new Inherited(value);
            return new Overrides(value);
        }

        public static int ValueOf(object value) => ((Registered)value).Value;
        public static void Suppress(object value) => System.GC.SuppressFinalize(value);

        public static int Finalized => finalized;
        public static int Total => total;
        public static int OverridesRan => overrides;
        public static object TakeResurrected()
        {
            object receiver = resurrected;
            resurrected = null;
            return receiver;
        }

        private class NewSlot
        {
#pragma warning disable CS0465 // A distinct slot named Finalize is the negative control.
            protected virtual void Finalize() { }
#pragma warning restore CS0465
        }

        private class Registered
        {
            internal readonly int Value;

            internal Registered(int value)
            {
                Value = value;
                if (value < 0) throw null;
            }

            ~Registered()
            {
                finalized++;
                total += Value;
            }
        }

        private sealed class Inherited : Registered
        {
            internal Inherited(int value) : base(value) { }
        }

        private sealed class Overrides : Registered
        {
            internal Overrides(int value) : base(value) { }
            ~Overrides()
            {
                overrides++;
                resurrected = this;
            }
        }
    }
}
