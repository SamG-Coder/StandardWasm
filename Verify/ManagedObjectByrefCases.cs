using ManagedObjectByrefs;

internal static class ManagedObjectByrefCases
{
    internal static void Add(List<object> cases)
    {
        for (int i = 0; i < 2; i++)
        {
            Check(nameof(Kernels.AcrossCall), [i], Kernels.AcrossCall(i));
            Check(nameof(Kernels.LocalAlias), [i], Kernels.LocalAlias(i));
            for (int j = 0; j < 2; j++)
                Check(nameof(Kernels.SharedElement), [i, j], Kernels.SharedElement(i, j));
        }

        foreach (int divisor in new[] { 0, 1, 10 })
            Check(nameof(Kernels.DuringUnwind), [divisor], Kernels.DuringUnwind(divisor));

        foreach (int index in new[] { int.MinValue, -1, 0, 1, 2, int.MaxValue })
            for (int useNull = 0; useNull <= 1; useNull++)
                Check(nameof(Kernels.AddressChecks), [index, useNull], Kernels.AddressChecks(index, useNull));

        foreach (int value in new[] { int.MinValue, 7, int.MaxValue })
        {
            Check(nameof(Kernels.TypedParameter), [value], Kernels.TypedParameter(value));
            for (int share = 0; share <= 1; share++)
                Check(nameof(Kernels.TypedLocal), [value, share], Kernels.TypedLocal(value, share));
        }

        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "ManagedObjectByrefs", method, inputs, expected });
    }
}
