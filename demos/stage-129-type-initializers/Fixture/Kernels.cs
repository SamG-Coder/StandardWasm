using System;
using TypeInitializerDependency;

namespace TypeInitializerFixture
{
    // Trace has no initializer. Each initializer appends one digit, so a kernel's
    // result shows which initializers ran and in what order. The CLR oracle loads
    // each kernel into a fresh AssemblyLoadContext; the runner instantiates a fresh
    // module per kernel, so neither side carries static state between cases.
    public static class Trace
    {
        public static int Log;

        public static void Add(int digit) => Log = Log * 10 + digit;

        public static int Mark(int digit)
        {
            Add(digit);
            return digit;
        }
    }

    // Precise: an explicit static constructor (no beforefieldinit).
    public static class Precise
    {
        public static int Value;

        static Precise()
        {
            Trace.Add(2);
            Value = 5;
        }

        public static int Touch(int argument) => argument + Value;
    }

    public static class PreciseStore
    {
        public static int Value;

        static PreciseStore()
        {
            Trace.Add(2);
            Value = 9;
        }
    }

    // Never reached at run time, but compiled into a kernel's untaken branch.
    // The security attribute gives this image a DeclSecurity table, which CoreLib lacks. The
    // bundle lists this image before CoreLib, so finding CoreLib's TypeInitializationException must
    // read each image's own table counts (a regression found in Codex's #306 review).
    [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Demand, UnmanagedCode = true)]
    public static class ThrowsObject
    {
        public static int Value = Fail();
        static int Fail() => throw new System.InvalidOperationException();
    }

    public static class Outer
    {
        public static class InnerThrows
        {
            public static int Value = Fail();
            static int Fail() => throw new System.NotSupportedException();
        }

        // A valid non-ASCII identifier: its UTF-8 metadata name becomes the UTF-16 FullName.
        public static class CaféFailure
        {
            public static int Value = Fail();
            static int Fail() => throw new System.InvalidOperationException();
        }
    }

    public static class Unrelated
    {
        public static int Value;

        static Unrelated()
        {
            Trace.Add(9);
            Value = 1;
        }
    }

    // beforefieldinit: only field initializers. The CLR may run it at any time
    // before the first field access, so kernels check only once-and-before-read.
    public static class Lazy
    {
        public static readonly int Value = Compute();
        public static int Runs;

        private static int Compute()
        {
            Runs++;
            return 41;
        }
    }

    public sealed class Constructed
    {
        public static int Count;

        static Constructed() => Trace.Add(3);

        public Constructed() => Count++;
    }

    public class BaseStatics
    {
        public static int Shared;

        static BaseStatics()
        {
            Trace.Add(4);
            Shared = 10;
        }
    }

    public class DerivedStatics : BaseStatics
    {
        static DerivedStatics() => Trace.Add(5);
    }

    public static class RecursiveA
    {
        public static int Value;

        static RecursiveA()
        {
            Trace.Add(6);
            // B's initializer reads A while A is still running: it sees zero.
            Value = RecursiveB.Value + 1;
        }
    }

    public static class RecursiveB
    {
        public static int Value;

        static RecursiveB()
        {
            Trace.Add(7);
            Value = RecursiveA.Value + 100;
        }
    }

    public static class Guarded
    {
        public static int Value;
        private static int zero;

        static Guarded()
        {
            try { Value = 1 / zero; }
            catch (DivideByZeroException) { Value = 77; }
            finally { Trace.Add(1); }
        }
    }

    public static class Failing
    {
        public static int Value;
        private static int zero;

        static Failing()
        {
            Trace.Add(8);
            Value = 1 / zero;
        }
    }

    public static class OuterFailing
    {
        public static int Value;

        static OuterFailing()
        {
            Trace.Add(6);
            Value = Failing.Value + 1;
        }
    }

    // Exhausts the Wasm stack inside an initializer: a raw engine trap that no
    // managed handler sees. The CLR would end the process, so this is a Wasm-only
    // check of the export boundary (decision 0057), not a CLR comparison.
    public static class Exhausting
    {
        public static int Value;

        static Exhausting()
        {
            Trace.Add(5);
            Value = Deep(0);
        }

        private static int Deep(int depth) => depth < 0 ? 0 : Deep(depth + 1) + 1;
    }

    public sealed class Empty
    {
    }

    public static class Kernels
    {
        private static bool never;
        private static object saved;
        private static readonly object gate = new object();

        // A reached initializer runs once; an initializer behind an untaken branch never runs.
        public static int ReachedOnce()
        {
            if (never) return Unrelated.Value;
            int first = Precise.Value, second = Precise.Value;
            return Trace.Log * 100 + first + second;
        }

        // A precise static call runs the initializer after evaluating its argument.
        public static int CallAfterArgument()
        {
            Trace.Add(1);
            int value = Precise.Touch(Trace.Mark(3));
            return Trace.Log * 100 + value;
        }

        // A static store runs the initializer after the right-hand side, before the write.
        public static int StoreAfterRightHand()
        {
            PreciseStore.Value = Trace.Mark(3);
            return Trace.Log * 100 + PreciseStore.Value;
        }

        public static int BeforeFieldInitOnce()
        {
            int first = Lazy.Value, second = Lazy.Value;
            return Lazy.Runs * 1000 + first + second;
        }

        public static int ConstructorTriggers()
        {
            new Constructed();
            new Constructed();
            return Trace.Log * 100 + Constructed.Count;
        }

        // C# binds DerivedStatics.Shared to BaseStatics::Shared: only the base initializes.
        public static int InheritedField()
        {
            int shared = DerivedStatics.Shared;
            return Trace.Log * 100 + shared;
        }

        public static int Recursion()
        {
            int value = RecursiveA.Value;
            return value * 100 + Trace.Log;
        }

        public static int CatchInsideInitializer() => Guarded.Value * 10 + Trace.Log;

        public static int BundledDependency()
        {
            int field = Registry.Value, call = Registry.Read();
            return Registry.Runs * 1000 + field + call;
        }

        // An escaping exception: the CLR raises TypeInitializationException.
        // A closed beforefieldinit generic static call that reads no static field.
        public static int RelaxedGenericCall() => RelaxedBox<int>.Id(4);

        // A closed precise generic static call stays a located diagnostic.
        public static int PreciseGenericCall() => PreciseBox<int>.Id(4);

        public static int ReadFailing() => Failing.Value;

        public static int ReadOuterFailing() => OuterFailing.Value;

        public static int ReadTrace() => Trace.Log;

        public static int ReadExhausting() => Exhausting.Value;

        // Root objects (System.Object allocations).
        private static object Fresh() => new object();

        public static int DistinctObjects() => Fresh() == Fresh() ? 0 : 1;

        public static int StaticFieldAlias()
        {
            object value = Fresh();
            saved = value;
            return saved == value ? 1 : 0;
        }

        public static int NotNominalClass() => Fresh() is Empty ? 0 : 1;

        // Issue #170: an initializer that throws a managed exception object. The CLR wraps it in
        // TypeInitializationException, raised again on every later trigger; the compiler builds
        // CoreLib's own TypeInitializationException when the bundle supplies CoreLib.
        public static int ObjectFailureCaught()
        {
            int score = 0;
            for (int i = 0; i < 2; i++)
            {
                try { score += ThrowsObject.Value; }
                catch (TypeInitializationException) { score += 10; }
                catch (InvalidOperationException) { score += 1000; }
            }
            return score + Unrelated.Value;
        }

        public static int NestedObjectFailure()
        {
            try { return Outer.InnerThrows.Value; }
            catch (TypeInitializationException) { return 3; }
        }

        public static int NonAsciiNameFailure()
        {
            try { return Outer.CaféFailure.Value; }
            catch (TypeInitializationException) { return 3; }
        }

        // Object members on a root object and Monitor stay explicit diagnostics.
        public static int RootHashCode() => Fresh().GetHashCode() == 0 ? 0 : 1;

        public static int RootToString() => Fresh().ToString().Length;

        public static int RootGetType() => Fresh().GetType() == typeof(object) ? 1 : 0;

        public static int RootEquals() => Fresh().Equals(saved) ? 0 : 1;

        public static int Locked()
        {
            lock (gate) { return 1; }
        }
    }
}
