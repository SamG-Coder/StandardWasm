using System;
using ReferencedDelegateDependency;

namespace ReferencedDelegateFixture
{
    public static class Kernels
    {
        static long Mix(long a, int b) => a * 10 + b;
        static string last;
        static void Keep(string text) => last = text;
        static int seen;

        static Flag StopAtThree(Handle window, IntPtr state)
        {
            seen = seen * 10 + (int)window.Value + (int)state;
            return new Flag { Value = (int)window.Value < 3 ? 1 : 0 };
        }

        // Constructed and invoked here; the types come from the dependency's TypeDefs.
        public static int StaticTarget() { Combine c = Mix; return (int)c(4, 2); }
        public static int VoidResult() { Sink s = Keep; s("four"); return last.Length; }
        public static int ClassResult() { Factory f = start => new Counter { Value = start }; return f(41).Next(); }
        public static int NullInvoke() { Combine c = null; try { return (int)c(1, 1); } catch (NullReferenceException) { return 9; } }

        // Constructed here, invoked by the dependency (its own MethodDef Invoke).
        public static int InvokedByDependency() { int k = 7; return (int)Library.Apply((a, b) => a * k + b, 3, 4); }
        public static int VoidInvokedByDependency() { Library.Send(Keep, "hello"); return last.Length; }

        // Constructed by the dependency, invoked here.
        public static int ConstructedByDependency() => (int)Library.Doubler()(20, 2);

        // A nested delegate over value-type wrappers, called back from the dependency.
        public static int NestedValueCallback()
        {
            seen = 0;
            int visited = Native.Enumerate(StopAtThree, (IntPtr)0);
            return visited * 1000 + seen; // 3123
        }

        // Diagnosed until decision 0092's nominal lists (Stage 221) give delegates a type identity.
        public static int RejectCastFromObject() { object o = (Combine)Mix; return (int)((Combine)o)(4, 2); }
    }
}
