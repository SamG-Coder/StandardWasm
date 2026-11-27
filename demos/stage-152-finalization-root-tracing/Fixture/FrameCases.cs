namespace ManagedFinalizationExperiment
{
    internal static class FrameCases
    {
        internal static void BeforeAndAfter(object receiver)
        {
            Pause();
            Observe(receiver);
            Pause();
        }

        internal static void ConditionalUse(object receiver, bool condition)
        {
            Pause();
            if (condition) Observe(receiver);
            Pause();
        }

        internal static void LoopUse(object receiver, int count)
        {
            while (count > 0)
            {
                Observe(receiver);
                Pause();
                count--;
            }
            Pause();
        }

        internal static void Replacement(object receiver, object replacement)
        {
            Pause();
            receiver = replacement;
            Pause();
            Observe(receiver);
        }

        internal static void SwitchUse(object receiver, int choice)
        {
            Pause();
            switch (choice)
            {
                case 0: Other(); break;
                case 1: Observe(receiver); break;
                case 2: Other(); Other(); break;
                case 3: return;
            }
            Pause();
        }

        internal static void TakeAddress(object receiver) => Clear(ref receiver);

        internal static void CatchUse(object receiver)
        {
            try { Pause(); }
            catch { Observe(receiver); }
        }

        private static void Clear(ref object value) => value = null;
        private static void Observe(object value) { }
        private static void Pause() { }
        private static void Other() { }
    }
}
