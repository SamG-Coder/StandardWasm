using System.Text.Json;
using DisplayCapabilities;

IntPtr context = Probe.GetDesktop();
if (context == IntPtr.Zero) throw new InvalidOperationException("Windows did not return a screen device context.");
var capabilities = new Dictionary<int, int>();
int release;
try
{
    foreach (int selector in new[] { 12, 14, 88, 90 })
        capabilities.Add(selector, Probe.Capability(context, selector));
}
finally
{
    release = Probe.ReleaseDesktop(context);
}
var result = new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    capabilities,
    release,
    nullCapability = Probe.Capability(IntPtr.Zero, 88),
    nullRelease = Probe.ReleaseDesktop(IntPtr.Zero)
};
File.WriteAllText(args[0], JsonSerializer.Serialize(result));
