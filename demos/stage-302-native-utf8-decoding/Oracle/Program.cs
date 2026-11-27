using System.Runtime.InteropServices;
using System.Text.Json;

namespace StandardWasm;

public static partial class Program
{
    private static readonly (string Name, byte[] Input)[] Cases =
    [
        ("null", []),
        ("empty", [0]),
        ("ascii", [0x41, 0x7a, 0]),
        ("twoByte", [0xc2, 0xa2, 0]),
        ("bmp", [0xe2, 0x82, 0xac, 0]),
        ("supplementary", [0xf0, 0x9f, 0x98, 0x80, 0]),
        ("validBoundaries", [0xe0, 0xa0, 0x80, 0xed, 0x9f, 0xbf, 0xf4, 0x8f, 0xbf, 0xbf, 0]),
        ("bom", [0xef, 0xbb, 0xbf, 0]),
        ("overlong", [0xc0, 0xaf, 0xe0, 0x80, 0x80, 0]),
        ("surrogate", [0xed, 0xa0, 0x80, 0]),
        ("outsideUnicode", [0xf4, 0x90, 0x80, 0x80, 0]),
        ("isolatedContinuation", [0x80, 0x41, 0]),
        ("invalidContinuation", [0xe1, 0x80, 0x41, 0xf1, 0x80, 0x41, 0]),
        ("truncatedPrefix", [0xf0, 0x9f, 0x98, 0]),
        ("firstNul", [0x41, 0, 0x42, 0]),
        ("mixed", [0x41, 0xc2, 0xa2, 0xf1, 0x80, 0xe2, 0x82, 0xac, 0])
    ];

    public static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
        File.WriteAllText(args[0], GetReport());
    }

    public static string GetReport() => JsonSerializer.Serialize(new
    {
        runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
        assembly = typeof(NativeUtf8Probe).Assembly.Location,
        row = typeof(NativeUtf8Probe).GetMethod(nameof(NativeUtf8Probe.Read))!.MetadataToken & 0xffffff,
        cases = Cases.Select(test => new { name = test.Name, input = test.Input.Select(b => (int)b).ToArray(), units = Units(test.Input) })
    });

#pragma warning disable CA1416 // This export is invoked only on the stock browser runtime.
    [System.Runtime.InteropServices.JavaScript.JSExport]
#pragma warning restore CA1416
    public static string GetStockText()
    {
        var text = new System.Text.StringBuilder();
        foreach (var test in Cases)
        {
            text.Append("UTF8|").Append(test.Name);
            foreach (int unit in Units(test.Input)) text.Append('|').Append(unit);
            text.Append('\n');
        }
        return text.ToString();
    }
    private static unsafe int[] Units(byte[] bytes)
    {
        fixed (byte* address = bytes)
        {
            int count = NativeUtf8Probe.Read((nint)address, -1);
            if (count < 0) return [-1];
            var units = new int[count];
            for (int index = 0; index < count; index++) units[index] = NativeUtf8Probe.Read((nint)address, index);
            return units;
        }
    }
}
