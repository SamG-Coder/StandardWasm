#:property PublishAot=false
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:project ./Win32Fixture/Win32Fixture.csproj
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length > 0 && args[0] == "dpi")
{
    int[] contexts = [0, -1, -2, -3, -4, -5, -6];
    int[] valid = contexts.Select(DpiProbe.DpiValid).ToArray();
    int[] awareness = contexts.Select(DpiProbe.DpiAwareness).ToArray();
    int initial = DpiProbe.DpiCurrent();
    var steps = new List<int[]>();
    foreach (int mode in args.Skip(1).Select(int.Parse))
        steps.Add([DpiProbe.DpiApply(mode), Win32ErrorProbe.Read(), DpiProbe.DpiCurrent(), DpiProbe.DpiIsAware()]);
    Console.WriteLine(JsonSerializer.Serialize(new { valid, awareness, initial, steps }));
    return;
}

string executable = Path.GetFileName(Environment.ProcessPath)!;
nint original = Lookup(null);
var results = new List<int[]>();
foreach (string? name in new[] { (string?)null, executable.ToUpperInvariant(),
    "missing-standardwasm-native-module.dll", "", executable + "\0suffix" })
{
    nint handle = Lookup(name);
    int error = Marshal.GetLastPInvokeError();
    results.Add([handle == 0 ? 0 : handle == original ? 1 : -1, error]);
}

Marshal.SetLastPInvokeError(73);
Marshal.SetLastSystemError(19);
int system = Marshal.GetLastSystemError();
int pinvoke = Marshal.GetLastPInvokeError();
Marshal.SetLastPInvokeError(0);
int[] compiledFixture = [Win32ErrorProbe.Exchange(19), Win32ErrorProbe.Exchange(-13), Win32ErrorProbe.Read(),
    Win32ErrorProbe.LookupModule(0), Win32ErrorProbe.LookupModule(1),
    Win32ErrorProbe.LookupModule(2), Win32ErrorProbe.LookupModule(3)];
var forms = typeof(System.Windows.Forms.Application).Module;
nint moduleHandle = Lookup(forms.Name.ToUpperInvariant());
int moduleError = Marshal.GetLastPInvokeError();
Console.WriteLine(JsonSerializer.Serialize(new { executable, results, errors = new[] { system, pinvoke },
    compiledFixture, compiledObjectSpans = new[] { ObjectCellSpanProbe.ObjectSpanAliases(0), ObjectCellSpanProbe.ObjectSpanAliases(1),
        ObjectCellSpanProbe.ObjectSpanBounds(-1), ObjectCellSpanProbe.ObjectSpanBounds(0), ObjectCellSpanProbe.ObjectSpanBounds(1) },
    compiledBooleanLocations = Enumerable.Range(0, 3).Select(BooleanLocationProbe.BooleanAliases)
        .Concat(Enumerable.Range(0, 2).Select(BooleanLocationProbe.BooleanWithMonitor)).ToArray(),
    compiledGenericValues = new[] { -1, 0, 1 }.Select(GenericValueProbe.ReadGenericValue).ToArray(),
    compiledFinalWriter = Enumerable.Range(0, 2).Select(FinalWriterProbe.InvokeFinalWriter).ToArray(),
    compiledGenericLocations = Enumerable.Range(0, 3).Select(GenericLocationProbe.GenericBooleanLocations)
        .Concat(Enumerable.Range(0, 3).Select(GenericLocationProbe.GenericStringLocations)).ToArray(),
    compiledFilter = Enumerable.Range(0, 4).Select(ExceptionFilterProbe.MatchPair).ToArray(), compiledMonitor = Enumerable.Range(0, 4).Select(MonitorProbe.LockRoundTrip)
        .Concat(Enumerable.Range(0, 3).Select(MonitorProbe.ReleaseDepth)).ToArray(), compiledStaticPin = new[] { PinnedStaticProbe.Sum(), PinnedStaticProbe.Alias(2),
        PinnedStaticProbe.Sum(), PinnedStaticProbe.Alias(9), PinnedStaticProbe.Sum() }, framework = forms.Assembly.Location, frameworkLookup = new[] { moduleHandle == 0 ? 0 : 1, moduleError },
    activationContext = ActivationContextOracle.Probe(moduleHandle), compiledActivation = ActivationContextOracle.ProbeFixture(), compiledLifecycle = ActivationContextProbe.Lifecycle(), compiledPointers = new[] { 0, -1, 123 }
        .SelectMany(value => new[] { ActivationContextProbe.PointerRoundTrip(value, 0), ActivationContextProbe.PointerRoundTrip(value, 1) }) }));

[DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true,
    CharSet = CharSet.Unicode, SetLastError = true)]
static extern nint Lookup(string? name);

// The upstream file-backed ThemingScope passes the module and a numeric
// manifest ID to the OS. Compare handle relationships instead of raw values.
static class ActivationContextOracle
{
    private const uint ModuleAndResource = 0x80 | 0x08;

    [StructLayout(LayoutKind.Sequential)]
    private struct ActivationContext
    {
        public uint Size, Flags;
        public nint Source;
        public ushort ProcessorArchitecture, Language;
        public nint AssemblyDirectory, Resource, ApplicationName, Module;
    }

    internal static object Probe(nint module)
    {
        if (module == 0) throw new InvalidOperationException("WinForms module is not mapped.");
        var request = new ActivationContext
        {
            Size = (uint)Marshal.SizeOf<ActivationContext>(),
            Flags = ModuleAndResource,
            Resource = 2,
            Module = module
        };
        nint context = CreateActCtxW(ref request);
        int createError = Marshal.GetLastPInvokeError();
        if (context == -1) throw new InvalidOperationException($"WinForms activation context failed with {createError}.");
        try
        {
            nint before = CurrentContext();
            bool activated = ActivateActCtx(context, out nuint cookie) != 0;
            if (!activated) throw new InvalidOperationException("WinForms activation failed.");
            bool currentMatches;
            try
            {
                currentMatches = CurrentContext() == context;
            }
            finally
            {
                if (DeactivateActCtx(0, cookie) == 0)
                    throw new InvalidOperationException("WinForms deactivation failed.");
            }
            bool restored = CurrentContext() == before;

            request.Resource = 65535;
            int missing = Failure(ref request);
            request.Resource = 2;
            request.Flags |= 0x80000000;
            int invalidFlags = Failure(ref request);
            return new
            {
                requestSize = request.Size,
                resourceOffset = Marshal.OffsetOf<ActivationContext>(nameof(ActivationContext.Resource)).ToInt32(),
                moduleOffset = Marshal.OffsetOf<ActivationContext>(nameof(ActivationContext.Module)).ToInt32(),
                createError, activated, currentMatches, restored, missing, invalidFlags
            };
        }
        finally
        {
            ReleaseActCtx(context);
        }
    }

    internal static int[][] ProbeFixture()
    {
        var results = new int[4][];
        for (int scenario = 0; scenario < results.Length; scenario++)
        {
            nint handle = ActivationContextProbe.Create(scenario);
            int error = Marshal.GetLastPInvokeError();
            results[scenario] = [handle == -1 ? -1 : 1, error];
            if (handle != -1) ReleaseActCtx(handle);
        }
        return results;
    }

    private static nint CurrentContext()
    {
        if (GetCurrentActCtx(out nint handle) == 0)
            throw new InvalidOperationException("GetCurrentActCtx failed.");
        if (handle != 0) ReleaseActCtx(handle);
        return handle;
    }

    private static int Failure(ref ActivationContext request)
    {
        nint unexpected = CreateActCtxW(ref request);
        int error = Marshal.GetLastPInvokeError();
        if (unexpected == -1) return error;
        ReleaseActCtx(unexpected);
        throw new InvalidOperationException("Unexpected successful activation context creation.");
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint CreateActCtxW(ref ActivationContext request);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int ActivateActCtx(nint context, out nuint cookie);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetCurrentActCtx(out nint context);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int DeactivateActCtx(uint flags, nuint cookie);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void ReleaseActCtx(nint context);
}
