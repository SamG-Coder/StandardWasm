using System.Globalization;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var cases = new List<object>();
void Record(string name, string input, bool wide, bool unsignedResult, Func<object> oracle)
{
    object expected;
    try { expected = oracle(); }
    catch (OverflowException) { expected = typeof(OverflowException).FullName!; }
    cases.Add(new { name, input, wide, unsignedResult, expected });
}

long[] signed = [long.MinValue, (long)int.MinValue - 1, int.MinValue, -1, 0, int.MaxValue, (long)int.MaxValue + 1, long.MaxValue];
ulong[] unsigned = [0, 1, int.MaxValue, (ulong)int.MaxValue + 1, uint.MaxValue, (ulong)uint.MaxValue + 1, ulong.MaxValue];
foreach (long value in signed)
    Record(nameof(Guest.Signed), value.ToString(CultureInfo.InvariantCulture), true, false, () => Guest.Signed(value));
foreach (ulong value in unsigned)
    Record(nameof(Guest.Unsigned), value.ToString(CultureInfo.InvariantCulture), true, true, () => Guest.Unsigned(value));
foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
{
    string input = value.ToString(CultureInfo.InvariantCulture);
    Record(nameof(Guest.Masked), input, false, false, () => Guest.Masked(value));
    Record(nameof(Guest.SignedPointer), input, false, false, () => Guest.SignedPointer(value));
}
foreach (uint value in new[] { 0u, 1u, (uint)int.MaxValue + 1, uint.MaxValue })
    Record(nameof(Guest.UnsignedPointer), value.ToString(CultureInfo.InvariantCulture), false, true, () => Guest.UnsignedPointer(value));
foreach (long value in new[] { (long)int.MinValue - 1, 17, (long)int.MaxValue + 1 })
    Record(nameof(Guest.Caught), value.ToString(CultureInfo.InvariantCulture), true, false, () => Guest.Caught(value));

File.WriteAllText(args[0], JsonSerializer.Serialize(new { clr = Environment.Version.ToString(), cases }));
