using InlineObjectAddressesFixture;

internal static class InlineObjectAddressCases
{
    internal static void Add(List<object> cases)
    {
        Check(nameof(Kernels.SharedAlias), [], Kernels.SharedAlias());
        Check(nameof(Kernels.DefaultKeepsAlias), [], Kernels.DefaultKeepsAlias());
        foreach (int choose in new[] { 0, 1 })
            Check(nameof(Kernels.CopyAndReplace), [choose], Kernels.CopyAndReplace(choose));
        foreach (int divisor in new[] { 0, 1, 7, -1 })
            Check(nameof(Kernels.Unwind), [divisor], Kernels.Unwind(divisor));

        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "InlineObjectAddressesFixture", method, inputs, expected });
    }
}
