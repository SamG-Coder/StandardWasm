using ObjectReadOnlySpansFixture;

internal static class ObjectReadOnlySpanCases
{
    internal static void Add(List<object> cases)
    {
        Check(nameof(Kernels.ObserveReset), [], Kernels.ObserveReset());
        Check(nameof(Kernels.EmptyLength), [], Kernels.EmptyLength());
        foreach (int index in new[] { 0, 1, 2 })
            Check(nameof(Kernels.ObserveElement), [index], Kernels.ObserveElement(index));
        foreach (int index in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue })
            for (int empty = 0; empty < 2; empty++)
                Check(nameof(Kernels.InvalidIndex), [index, empty], Kernels.InvalidIndex(index, empty));

        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "ObjectReadOnlySpansFixture", method, inputs, expected });
    }
}
