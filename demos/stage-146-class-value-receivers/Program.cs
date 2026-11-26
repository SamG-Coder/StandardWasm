using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text.Json;

var cases = new (string Method, int Input)[]
{
    (nameof(Kernels.MethodUnwind), 0), (nameof(Kernels.MethodUnwind), 1),
    (nameof(Kernels.AddressComesFirst), 0), (nameof(Kernels.AddressComesFirst), 1),
    (nameof(Kernels.RealSection), -1), (nameof(Kernels.RealSection), 0),
    (nameof(Kernels.RealSection), 255), (nameof(Kernels.RealSection), 256),
};

// Validate the explicit overlay against the installed CLR's original factory.
// The Wasm path uses the same sections to isolate the setter from the factory's generic helper.
var first = BitVector32.CreateSection(255);
var next = BitVector32.CreateSection(15, first);
if (Kernels.Section(255) != first || Kernels.Section((8 << 16) | 15) != next)
    throw new InvalidOperationException("Section layout differs from the installed CLR");

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

public struct Counter
{
    public int Value;
    public int WriteThenDivide(int divisor)
    {
        Value = 41;
        return 5 / divisor;
    }
}

public sealed class Owner
{
    public Counter First;
    public Counter Second;
}

public sealed class FrameworkOwner
{
    public BitVector32 Bits;
}

[StructLayout(LayoutKind.Explicit)]
public struct SectionView
{
    [FieldOffset(0)] public int Bits;
    [FieldOffset(0)] public BitVector32.Section Value;
}

public static class Kernels
{
    public static int MethodUnwind(int divisor)
    {
        var owner = new Owner();
        owner.Second = new Counter { Value = 9 };
        int result;
        try { result = owner.First.WriteThenDivide(divisor); }
        catch (DivideByZeroException) { result = 7; }
        return result * 10000 + owner.First.Value * 100 + owner.Second.Value;
    }

    private static int ReadAndMark(Owner trace)
    {
        trace.First = new Counter { Value = 17 };
        return 1;
    }

    public static int AddressComesFirst(int available)
    {
        var trace = new Owner();
        Owner receiver = available == 0 ? null : new Owner();
        try { return receiver.First.WriteThenDivide(ReadAndMark(trace)) + trace.First.Value; }
        catch (NullReferenceException) { return trace.First.Value; }
    }

    internal static BitVector32.Section Section(int bits)
    {
        SectionView view = new SectionView { Bits = bits };
        return view.Value;
    }

    public static int RealSection(int input)
    {
        var owner = new FrameworkOwner();
        BitVector32.Section target = Section(255);
        BitVector32.Section neighbor = Section((8 << 16) | 15);
        owner.Bits[neighbor] = 9;
        owner.Bits[target] = input;
        return owner.Bits[target] * 16 + owner.Bits[neighbor];
    }
}
