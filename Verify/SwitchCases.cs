using SwitchDispatch;

// CLR oracle for stage 25. It covers every case label of every jump table, the values just outside each range,
// int.MinValue/MaxValue (whose biased index wraps), and seeded random inputs.
internal static class SwitchCases
{
    internal static void Add(List<object> cases)
    {
        void Record(string method, int[] inputs, Func<int> run) =>
            cases.Add(new { module = "SwitchDispatch", method, inputs, expected = run() });

        var edges = new[] { int.MinValue, int.MinValue + 1, -1, int.MaxValue - 1, int.MaxValue };
        foreach (int value in Enumerable.Range(-6, 90).Concat(edges))
        {
            Record("DayKind", [value], () => Dispatch.DayKind(value));
            Record("Biased", [value], () => Dispatch.Biased(value));
            Record("Weight", [value], () => Dispatch.Weight(value));
            Record("Wide", [value], () => Dispatch.Wide(value));
        }
        foreach (int start in new[] { 0, 1, 2, 3, 27, 97, -5, int.MaxValue, int.MinValue })
            foreach (int steps in new[] { 0, 1, 7, 100 })
                Record("Collatzish", [start, steps], () => Dispatch.Collatzish(start, steps));
        // Swap one character of the sample for each character class, including invalid ones.
        foreach (int at in new[] { -1, 0, 1, 2, 3, 6, 7, 12, 13 })
            foreach (int replacement in new[] { 'a', 'z', '0', '9', ' ', '+', '*', '-', '{', 0, -1 })
                Record("TokenizeSample", [at, replacement], () => Dispatch.TokenizeSample(at, replacement));

        var random = new Random(2525);
        for (int i = 0; i < 300; i++)
        {
            int value = i % 2 == 0 ? random.Next(-100, 100) : random.Next(int.MinValue, int.MaxValue);
            Record("Biased", [value], () => Dispatch.Biased(value));
            Record("Wide", [value], () => Dispatch.Wide(value));
            Record("Collatzish", [value, i % 50], () => Dispatch.Collatzish(value, i % 50));
        }
    }
}
