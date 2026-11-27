namespace ManagedFinalizationExperiment
{
    internal sealed class Receiver
    {
        internal Receiver Child;
        internal int Value;
        internal int RegistrationCount;
        internal bool Suppressed;
        internal int Mark;
    }

    public static class Experiment
    {
        private static int Push(Receiver value, int epoch, Receiver[] work, int count)
        {
            if (value == null || value.Mark == epoch) return count;
            value.Mark = epoch;
            work[count] = value;
            return count + 1;
        }

        // Registered instances are retained by the runtime so their state is still available.
        // Only ordinary roots and pending finalizers participate in the first mark.
        private static int Discover(Receiver[] registrations, Receiver root, Receiver[] queue, int pending, int epoch)
        {
            var work = new Receiver[registrations.Length + 2];
            int count = Push(root, epoch, work, 0);
            for (int i = 0; i < pending; i++) count = Push(queue[i], epoch, work, count);
            for (int i = 0; i < count; i++) count = Push(work[i].Child, epoch, work, count);

            for (int i = 0; i < registrations.Length; i++)
            {
                Receiver value = registrations[i];
                if (value.Mark == epoch) continue;
                while (value.RegistrationCount != 0)
                {
                    value.RegistrationCount--;
                    if (value.Suppressed) value.Suppressed = false;
                    else
                    {
                        queue[pending] = value;
                        pending++;
                    }
                }
            }
            return pending;
        }

        private static void Suppress(Receiver value)
        {
            value.Suppressed = true;
        }

        private static void Register(Receiver value)
        {
            // CoreCLR clears an outstanding suppression before adding another registration.
            if (value.Suppressed) value.Suppressed = false;
            else value.RegistrationCount++;
        }

        public static int Run(int scenario)
        {
            var first = new Receiver { Value = 17, RegistrationCount = 1 };
            var second = new Receiver { Value = 23, RegistrationCount = 1 };
            var child = new Receiver { Value = 41 };
            child.Child = first;
            first.Child = child;
            var registrations = new Receiver[] { first, second };
            var queue = new Receiver[3];
            Receiver root = null;

            if (scenario == 0)
            {
                Suppress(first);
                Suppress(first);
            }
            if (scenario == 1)
            {
                Suppress(second);
                root = child;
            }
            if (scenario == 2)
            {
                Suppress(first);
                Register(first);
                Register(first);
                Suppress(second);
            }
            if (scenario == 3 || scenario == 4) Suppress(second);
            if (scenario == 5) first.Child = second;
            if (scenario == 6) root = second;

            int pending = Discover(registrations, root, queue, 0, 1);
            if (scenario == 3) Suppress(first);
            if (scenario == 4)
            {
                Suppress(first);
                Register(first);
                pending = Discover(registrations, null, queue, pending, 2);
            }
            if (scenario == 6)
            {
                // The pending receiver now owns the only path to the second object.
                first.Child = second;
                pending = Discover(registrations, null, queue, pending, 2);
            }

            int result = 0;
            for (int i = 0; i < pending; i++)
            {
                Receiver value = queue[i];
                if (value.Suppressed) value.Suppressed = false;
                else
                {
                    result += value.Value;
                    if (value.Child != null) result += value.Child.Value;
                    if (scenario == 2) root = value;
                }
                queue[i] = null;
            }
            if (scenario == 2)
            {
                child.Child = null;
                root.Child.Value += 3;
                result += Discover(registrations, root, queue, 0, 2);
                result += root.Child.Value;
            }
            if (scenario == 6)
            {
                // Only after the first callback returns can the second object be discovered.
                result += 100 * Discover(registrations, null, queue, 0, 3);
            }
            return result;
        }
    }
}
