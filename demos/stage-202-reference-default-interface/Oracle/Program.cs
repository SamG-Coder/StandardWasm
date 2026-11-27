using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ReferenceDefaultInterface;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.DefaultIdentity, Probe.SharedDefault, Probe.ClassPriority,
    Probe.ExplicitPriority, Probe.NullReceiver, Probe.ScalarDefaultBody, Probe.ForwardedBody, Probe.MoreSpecificDefault];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run.Method.Name == nameof(Probe.MoreSpecificDefault) ? new
        {
            status = -2,
            location = new[] { 0, typeof(Probe).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!.MetadataToken & 0xffffff, 2 }
        } : null
    })
}));
