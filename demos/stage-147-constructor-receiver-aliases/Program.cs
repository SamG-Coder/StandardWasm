using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

var cases = new (string Method, int Input)[]
{
    (nameof(Kernels.LinkedPadding), 0), (nameof(Kernels.LinkedPadding), 7),
    (nameof(Kernels.LinkedPadding), -5), (nameof(Kernels.LinkedPadding), int.MaxValue),
    (nameof(Kernels.ChainedAssignment), 0), (nameof(Kernels.ChainedAssignment), 7),
    (nameof(Kernels.ChainedAssignment), int.MinValue),
};

var results = cases.Select(test =>
{
    var method = typeof(Kernels).GetMethod(test.Method)!;
    return new
    {
        method = test.Method,
        row = method.MetadataToken & 0xffffff,
        arguments = new[] { test.Input },
        value = (int)method.Invoke(null, new object[] { test.Input })!,
    };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new { architecture = RuntimeInformation.ProcessArchitecture.ToString(), results }));

public struct Chained
{
    public int Left;
    public int Right;
    public int Total;

    public Chained(int input)
    {
        Left = Right = Total = input;
        Total = Left = Right + 1;
    }
}

public static class Kernels
{
    public static int LinkedPadding(int input)
    {
        var padding = new Padding(input);
        return padding.Left + 3 * padding.Top + 5 * padding.Right + 7 * padding.Bottom;
    }

    public static int ChainedAssignment(int input)
    {
        var value = new Chained(input);
        return value.Left + 3 * value.Right + 5 * value.Total;
    }
}
