using System;
using System.IO;
using System.Runtime.CompilerServices;
using MissingDependency;

namespace MissingDependencyFixture
{
    // Issue #299: the dependency assembly is absent at run time. The CLR's JIT fails each method
    // whose body needs it, and the call throws FileNotFoundException; untouched code still runs.
    public static class Kernels
    {
        sealed class HoldsReference { public Widget Field; public int Plain = 5; }
        sealed class HoldsValue { public Point P; public int Plain = 5; }
        sealed class Derived : BaseThing { }

        [MethodImpl(MethodImplOptions.NoInlining)] static int UsesWidget() => new Widget().Value;
        [MethodImpl(MethodImplOptions.NoInlining)] static int CallsStatic() => Widget.Twice(4);
        [MethodImpl(MethodImplOptions.NoInlining)] static int UsesDerived() => new Derived().Base;
        [MethodImpl(MethodImplOptions.NoInlining)] static int ValueField() => new HoldsValue().Plain;
        [MethodImpl(MethodImplOptions.NoInlining)] static int TakesWidget(Widget widget) => 3;
        [MethodImpl(MethodImplOptions.NoInlining)] static int PassesNull() => TakesWidget(null);
        [MethodImpl(MethodImplOptions.NoInlining)] static int BranchNeverTaken(int x) => x > 100 ? new Widget().Value : 11;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int CatchesInside()
        {
            try { return new Widget().Value; }
            catch (Exception) { return -100; }
        }

        // Code that needs nothing from the dependency.
        public static int Untouched() => 42;
        public static int ReferenceFieldOnly() => new HoldsReference().Plain;
        public static int ReferenceLocalOnly()
        {
            Widget widget = null;
            return widget == null ? 6 : 0;
        }

        // selection picks the failing callee; the caller catches what its call raises.
        public static int Caught(int selection)
        {
            try
            {
                if (selection == 0) return UsesWidget();
                if (selection == 1) return CallsStatic();
                if (selection == 2) return UsesDerived();
                if (selection == 3) return ValueField();
                if (selection == 4) return PassesNull();
                return BranchNeverTaken(1);
            }
            catch (FileNotFoundException) { return 100 + selection; }
            catch (Exception) { return -1; }
        }

        public static int CaughtAsException()
        {
            try { return UsesWidget(); }
            catch (Exception) { return 4; }
        }

        // The failing method's own handler never runs; its caller catches the exception.
        public static int InsideHandlerSkipped()
        {
            try { return CatchesInside(); }
            catch (FileNotFoundException) { return 5; }
        }

        // Every call throws again.
        public static int Repeated()
        {
            int hits = 0;
            for (int i = 0; i < 3; i++)
            {
                try { UsesWidget(); }
                catch (FileNotFoundException) { hits++; }
            }
            return hits;
        }

        // Code before the failing call runs, and the exception leaves this method.
        public static int Partial(int value) => value + Untouched() + UsesWidget();
    }
}
