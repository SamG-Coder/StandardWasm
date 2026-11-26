using System.Runtime.InteropServices;
using System.Text.Json;
using CoreRootProbe;

var core = typeof(object).Assembly;
if (typeof(MarshalByRefObject).BaseType != typeof(object) || core.GetName().Name != "System.Private.CoreLib")
    throw new InvalidOperationException("Expected the Microsoft CoreLib root.");
int[] arguments = [int.MinValue, -1, 17, int.MaxValue];
string app = typeof(Entry).Assembly.Location;
string facade = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "netstandard.dll");
string runtime = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll");
File.WriteAllText(args.Single(), JsonSerializer.Serialize(new
{
    runtime = RuntimeInformation.FrameworkDescription,
    coreIdentity = core.GetName().FullName,
    core = core.Location,
    facade,
    runtimeFacade = runtime,
    app,
    method = typeof(Entry).GetMethod(nameof(Entry.Run))!.MetadataToken & 0xffffff,
    arguments,
    expected = arguments.Select(Entry.Run)
}));
