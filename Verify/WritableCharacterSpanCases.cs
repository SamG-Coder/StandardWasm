using WritableCharacterSpansFixture;

internal static class WritableCharacterSpanCases
{
    internal static void Add(List<object> cases)
    {
        for (int index = 0; index < 3; index++)
            foreach (int value in new[] { 0, 65535, 65536 })
                Check(nameof(Kernels.BufferAliasing), [index, value], Kernels.BufferAliasing(index, value));

        for (int kind = 0; kind < 2; kind++)
        {
            Check(nameof(Kernels.IndependentSlots), [kind], Kernels.IndependentSlots(kind));
            Check(nameof(Kernels.LengthOfEmpty), [kind], Kernels.LengthOfEmpty(kind));
        }

        foreach (int index in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue })
            for (int empty = 0; empty < 2; empty++)
                Check(nameof(Kernels.CheckOrder), [index, empty], Kernels.CheckOrder(index, empty));

        foreach (int divisor in new[] { 0, 1, 10 })
            Check(nameof(Kernels.SurvivesUnwind), [divisor], Kernels.SurvivesUnwind(divisor));

        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "WritableCharacterSpansFixture", method, inputs, expected });
    }
}
