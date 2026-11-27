using System;
using System.IO;
using System.Text.Json;
using CoreVersionOutFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var version = Environment.OSVersion.Version;
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), core },
    row = typeof(Probe).GetMethod(nameof(Probe.Build)).MetadataToken & 0xffffff,
    expected = Probe.Build(),
    version = new[] { version.Major, version.Minor, version.Build, (int)Environment.OSVersion.Platform }
}));
