using System;
using System.Runtime.CompilerServices;

namespace ManagedFinalizationExperiment
{
    public static class InteriorOwnerProbe
    {
        private static int finalized;

        public static int ReadInteriorFinalizations() => finalized;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ObserveArrayInterior(int seed)
        {
            var entries = new Entry<Child>[2];
            InitializeArray(entries, seed);
            return Borrow(ref entries[0]);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ObserveClassInterior(int seed)
        {
            var owner = new Holder();
            if (seed != 0) InitializeClass(owner, seed);
            return Borrow(ref owner.Value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ObserveProjectedArrayInterior(int seed)
        {
            var entries = new Entry<Entry<Child>>[2];
            InitializeProjectedArray(entries, seed);
            return Borrow(ref entries[0].Reference);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeProjectedArray(Entry<Entry<Child>>[] entries, int seed)
        {
            entries[1] = new Entry<Entry<Child>> { Reference = new Entry<Child> { Reference = new Child() } };
            entries[0] = new Entry<Entry<Child>> { Reference = new Entry<Child> { Number = seed } };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeArray(Entry<Child>[] entries, int seed)
        {
            // The finalizable object is in the array, outside the addressed slot.
            entries[1] = new Entry<Child> { Reference = new Child() };
            if (seed != 0) entries[0] = new Entry<Child> { Number = seed };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeClass(Holder owner, int seed) =>
            owner.Value = new Entry<int> { Number = seed };

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Borrow<T>(ref Entry<T> entry)
        {
            Observe();
            return entry.Number;
        }

        // The managed caller publishes the alias before running the observer.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Observe() => Console.Out.WriteLine();

        private struct Entry<T>
        {
            public T Reference;
            public int Number;
        }

        private sealed class Child
        {
            ~Child() { finalized++; }
        }

        private sealed class Holder
        {
            public Entry<int> Value;
            ~Holder() { finalized++; }
        }
    }
}
