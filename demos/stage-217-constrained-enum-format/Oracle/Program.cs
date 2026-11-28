using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ConstrainedEnumFormat;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var probe = typeof(Probe);
var cases = probe.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(test =>
{
    string name = test.Name;
    int row = test.MetadataToken & 0xffffff;
    int expected = (int)test.Invoke(null, null)!;
    string? unsupported = name == "RejectUnsortedAliases" ? "ReadUnsortedAlias" : name == "RejectFlags" ? "ReadFlags" : null;
    object? rejection = null;
    if (unsupported is not null)
    {
        var body = probe.GetMethod(unsupported, BindingFlags.NonPublic | BindingFlags.Static)!;
        rejection = new { status = -2, location = new[] { 0, body.MetadataToken & 0xffffff, 0 } };
    }
    return new { name, row, expected, rejection };
}).ToArray();
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = probe.Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases
}));
