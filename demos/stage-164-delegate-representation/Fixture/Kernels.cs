using System;

namespace DelegateFixture
{
    public sealed class Counter
    {
        public event EventHandler Changed;
        public Func<int> Source;
        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
        public void Handler(object sender, EventArgs e) { }
        public int Read() => Source == null ? 1 : 2;
    }

    // Stage 164 (issue #256, decision 0088): one representation per delegate type wherever it is
    // declared, the C# cached-lambda pattern, and delegates over static methods. Every kernel
    // returns an int that the x86 CLR also returns.
    public static class Kernels
    {
        static int raised;
        static Func<int> stored;

        static int Run(Func<int> f) => f();
        static int Five() => 5;
        static int Seven() => 7;
        static void OnChanged(object sender, EventArgs e) => raised += sender is Counter ? 10 : 1;

        // A non-capturing lambda: C# caches it in a static Func<int> field of the <>c class.
        public static int CachedLambda() => Run(() => 5);

        // The cached field is reused: the second call takes the delegate already stored.
        public static int CachedLambdaTwice()
        {
            int total = 0;
            for (int i = 0; i < 2; i++) total += Run(() => 4);
            return total;
        }

        public static int CachedLambdaInTry()
        {
            try { return Run(() => 6); }
            catch (Exception) { return -1; }
        }

        // Method groups over static methods: ldnull; ldftn static; newobj.
        public static int StaticMethodGroup() { Func<int> f = Five; return f() + 1; }

        public static int StaticInStaticField() { stored = Seven; return stored() * 10 + Run(stored); }

        public static int StaticInInstanceField()
        {
            var counter = new Counter { Source = Five };
            return counter.Source() + Run(counter.Source);
        }

        public static int StaticEventHandler()
        {
            raised = 0;
            var counter = new Counter();
            counter.Changed += OnChanged;
            counter.Raise();
            counter.Raise();
            counter.Changed -= OnChanged;
            counter.Raise();
            return raised;
        }

        // Instance targets keep working beside static ones.
        public static int MixedTargets()
        {
            int x = 3;
            Func<int> captured = () => x * 2;
            Func<int> fixedValue = Five;
            return captured() * 10 + fixedValue();
        }

        public static int NullDelegateInvoke()
        {
            Func<int> f = null;
            try { return f(); }
            catch (NullReferenceException) { return 9; }
        }

        // The CLR rejects an instance delegate over a null target with ArgumentException at
        // construction (decision 0088 keeps that; an open static target binds null instead). The
        // delegate is used, so the construction cannot be optimized away on the CLR either.
        public static int NullInstanceTargetRejected()
        {
            Counter none = null;
            try
            {
                EventHandler h = none.Handler;
                h(null, EventArgs.Empty); // the body never touches `this`
                return 1;
            }
            catch (NullReferenceException) { return 11; }
            catch (ArgumentException) { return 12; }
        }

        // The same rule when the target would dereference `this`: construction fails first.
        public static int NullTargetFaultsOnUse()
        {
            Counter none = null;
            Func<int> f;
            try { f = none.Read; }
            catch (ArgumentException) { return 12; }
            try { return f(); }
            catch (NullReferenceException) { return 13; }
        }
    }
}
