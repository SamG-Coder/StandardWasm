using ManagedFinalizationExperiment;

internal static class InteriorOwnerCases
{
    internal static object Verify()
    {
        TextWriter original = Console.Out;
        try
        {
            var observer = new CollectingWriter();
            Console.SetOut(observer);
            return new[]
            {
                Check(InteriorOwnerProbe.ObserveArrayInterior, 0),
                Check(InteriorOwnerProbe.ObserveArrayInterior, 23),
                Check(InteriorOwnerProbe.ObserveClassInterior, 0),
                Check(InteriorOwnerProbe.ObserveClassInterior, 47),
                Check(InteriorOwnerProbe.ObserveProjectedArrayInterior, 71)
            };

            object Check(Func<int, int> run, int seed)
            {
                int before = InteriorOwnerProbe.ReadInteriorFinalizations();
                int value = run(seed);
                int during = observer.Finalized - before;
                Collect();
                int after = InteriorOwnerProbe.ReadInteriorFinalizations() - before;
                if (value != seed || during != 0 || after != 1)
                    throw new Exception("The CLR finalized the interior owner or its child at the wrong time");
                return new { value, during, after };
            }
        }
        finally { Console.SetOut(original); }
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private sealed class CollectingWriter : StringWriter
    {
        internal int Finalized;

        public override void WriteLine()
        {
            Collect();
            Finalized = InteriorOwnerProbe.ReadInteriorFinalizations();
        }
    }
}
