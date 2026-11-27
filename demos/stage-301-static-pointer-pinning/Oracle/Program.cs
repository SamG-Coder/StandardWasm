using System.Text.Json;
using StaticPointerPinning;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
int Row(int token) => token & 0xffffff;
Func<int>[] probes = [HelperWrite.Read, InlineWrite.Read, EscapingAddress.Read];
int rejectingInitializer = Row(typeof(EscapingAddress).TypeInitializer!.MetadataToken);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(HelperWrite).Assembly.Location,
    cases = probes.Select(run => new
    {
        name = run.Method.DeclaringType!.Name,
        row = Row(run.Method.MetadataToken),
        expected = run(),
        memory = true,
        rejection = run.Method.DeclaringType == typeof(EscapingAddress)
            ? new { status = -2, location = new[] { 0, rejectingInitializer, 8 } } : null
    })
}));
