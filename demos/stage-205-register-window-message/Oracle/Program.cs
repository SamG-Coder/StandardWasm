using System.Text.Json;

internal static unsafe class Program
{
    private const int SystemError = 0x5a5a;
    private const int SavedError = 0x6b6b;
    private const string Prefix = "StandardWasm.WindowMessages.Stage205.";

    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("The oracle requires Windows x86 .NET.");
        string primary = Prefix + "Case";
        string boundary = Prefix.PadRight(255, 'Z');
        Input[] inputs =
        [
            new(primary), new(primary), new(primary.ToUpperInvariant()), new(primary + "\0suffix"),
            new(Prefix + "distinct"), new(Prefix + "éclair"), new(Prefix.ToUpperInvariant() + "éCLAIR"),
            new(Prefix + "Éclair"), new(Prefix + "宇宙"), new(Prefix + "宇宙"),
            new("#123"), new("#000123"), new("#0"), new("#49151"), new("#49152"),
            new("#65535"), new("#65536"), new("#4294967297"), new("#4294967296"),
            new("#4295016447"), new("#" + new string('0', 254) + "1"),
            new("#+123"), new("#-123"), new("#123tail"), new("#text"), new("#TEXT"),
            new(primary, OddAddress: true), new(""), new(null), new(boundary + "Z"), new(boundary),
            new(primary, true), new("", true), new(null, true), new(boundary + "Z", true)
        ];
        var cases = inputs.Select(input => new { input, output = Probe(input) });
        File.WriteAllText(args[0], JsonSerializer.Serialize(new { system = SystemError, saved = SavedError, cases },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    private static Outcome Probe(Input input)
    {
        if (input.Text is null) return Call(0, input.CaptureError);
        int offset = input.OddAddress ? 1 : 0;
        Span<byte> storage = stackalloc byte[(input.Text.Length + 1) * sizeof(char) + offset];
        storage.Clear();
        for (int index = 0; index < input.Text.Length; index++)
        {
            int at = offset + index * sizeof(char);
            storage[at] = (byte)input.Text[index];
            storage[at + 1] = (byte)(input.Text[index] >> 8);
        }
        fixed (byte* start = storage)
            return Call((nint)(start + offset), input.CaptureError);
    }

    private static Outcome Call(nint address, bool captureError)
    {
        Guest.SetErrors(SystemError, SavedError);
        uint id = Guest.Register(address, captureError);
        return new(id, Guest.Error(), Guest.SavedError());
    }

    private sealed record Input(string? Text, bool CaptureError = false, bool OddAddress = false);
    private sealed record Outcome(uint Id, int Error, int Saved);
}
