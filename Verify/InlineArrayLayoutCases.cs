using System.Runtime.CompilerServices;
using InlineArrayLayouts;

internal static class InlineArrayLayoutCases
{
    internal static void Add(List<object> cases)
    {
        if (Unsafe.SizeOf<Words>() != 3 * sizeof(int) || Unsafe.SizeOf<Slots<object>>() != 3 * IntPtr.Size)
            throw new InvalidOperationException("The CLR did not expand the inline array fixtures.");

        Check(nameof(Kernels.DefaultFirst), [], Kernels.DefaultFirst());
        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            Check(nameof(Kernels.CopyFirst), [value, 7], Kernels.CopyFirst(value, 7));
            Check(nameof(Kernels.PassByValue), [value], Kernels.PassByValue(value));
            Check(nameof(Kernels.GenericReferenceSlots), [value], Kernels.GenericReferenceSlots(value));
        }

        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "InlineArrayLayouts", method, inputs, expected });
    }
}
