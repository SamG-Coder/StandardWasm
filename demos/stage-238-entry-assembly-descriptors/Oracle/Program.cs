using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Dependency = EntryAssemblyDependency.Probe;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrEmpty(packages))
            packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
            Path.Combine(packages, "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(),
                "runtimes", "browser-wasm", "native", "System.Private.CoreLib.dll");
        if (!File.Exists(core)) throw new FileNotFoundException("The matching stock browser Mono CoreLib is required.", core);
        Func<int>[] cases = [ProgramProbe.DefaultEntry, ProgramProbe.OverrideAndRestore, ProgramProbe.ExplicitNull];
        File.WriteAllText(args[0], JsonSerializer.Serialize(new
        {
            runtime = Environment.Version.ToString(),
            assembly = typeof(ProgramProbe).Assembly.Location,
            dependencies = new[] { typeof(Dependency).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"),
                Path.Combine(runtime, "netstandard.dll"), Path.GetFullPath(core) },
            cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
        }));
    }
}
