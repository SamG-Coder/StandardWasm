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
    compiledGenericValueMethods = Enumerable.Range(0, 3).Select(GenericValueMethodProbe.VerifyGenericValueMethod)
        .Concat(Enumerable.Range(0, 3).Select(GenericValueMethodProbe.VerifyGenericDefaultReference)).ToArray(),
    compiledByteSpan = Enumerable.Range(-1, 6).Select(StaticByteSpanProbe.ReadStaticByteSpan).ToArray(),
    compiledByteLocations = Enumerable.Range(0, 3).Select(StaticByteSpanProbe.ReadStaticByteLocation).ToArray(),
    compiledWhitespace = new[] { 0, 32, 133, 160, 5760, 8192, 8203, 8232, 12288, 65535 }
        .Select(StaticByteSpanProbe.CheckCoreWhitespace).ToArray(),
    compiledStringField = Enumerable.Range(0, 5).Select(CoreStringFieldProbe.CheckCoreStringFirstCharacter).ToArray(),
    compiledStringTrim = Enumerable.Range(0, 8).Select(CoreStringFieldProbe.CheckCoreStringTrim).ToArray(),
    scopedWritableSpan = new[] { 0, 65, 0x2000, 0xffff }.Select(ScopedSpanProbe.ScopedWritableSpan).ToArray(),
    stackBackedSpan = Enumerable.Range(0, 6).Select(ScopedSpanProbe.CheckStackBackedSpan).ToArray(),
    scopedReference = new[] { 0, -1, 65, 0xffff }.Select(ScopedSpanProbe.CheckScopedReference).ToArray(),
    debuggerStatus = DebuggerProbe.ReadManagedDebuggerStatus(),
    exactValueReads = new[] { int.MinValue, -5, 0, 5, int.MaxValue }.Select(ExactValueProbe.ExactValueRead).ToArray(),
    enumVectors = Enumerable.Range(0, 5).Select(EnumFieldDemo.Kernels.EnumVectors).ToArray(),
    characterCallbacks = new[] { -1, 0, 1, 2, 17 }.Select(SpanCallbackProbe.CheckCharacterCallback).ToArray(),
    staticEventCallbacks = Enumerable.Range(0, 3).Select(SpanCallbackProbe.CheckStaticEventCallback).ToArray(),
    compiledTypeReturn = Enumerable.Range(0, 2).Select(TypeReturnProbe.CompareReturnedType).ToArray(),
    compiledRootDispatch = Enumerable.Range(0, 4).Select(RootDispatchProbe.CompareThroughRoot).ToArray(),
    compiledValueAliases = Enumerable.Range(0, 3).Select(ValueAliasProbe.CheckValueAlias).ToArray(),
    compiledGenericDescriptors = Enumerable.Range(0, 3).Select(GenericDescriptorProbe.CheckGenericDescriptors).ToArray(),
    compiledConstrainedReferences = Enumerable.Range(0, 3).Select(ConstrainedReferenceProbe.InvokeConstrainedReceiver)
        .Concat(Enumerable.Range(0, 3).Select(ConstrainedReferenceProbe.CheckWidenedIdentity)).ToArray(),
    compiledGenericValues = new[] { -1, 0, 1 }.Select(GenericValueProbe.ReadGenericValue).ToArray(),
    compiledFinalWriter = Enumerable.Range(0, 2).Select(FinalWriterProbe.InvokeFinalWriter).ToArray(),
    compiledGenericLocations = Enumerable.Range(0, 3).Select(GenericLocationProbe.GenericBooleanLocations)
        .Concat(Enumerable.Range(0, 3).Select(GenericLocationProbe.GenericStringLocations)).ToArray(),
    compiledFilter = Enumerable.Range(0, 4).Select(ExceptionFilterProbe.MatchPair).ToArray(), compiledMonitor = Enumerable.Range(0, 4).Select(MonitorProbe.LockRoundTrip)
        .Concat(Enumerable.Range(0, 3).Select(MonitorProbe.ReleaseDepth)).ToArray(), compiledStaticPin = new[] { PinnedStaticProbe.Sum(), PinnedStaticProbe.Alias(2),
        PinnedStaticProbe.Sum(), PinnedStaticProbe.Alias(9), PinnedStaticProbe.Sum() }, framework = forms.Assembly.Location, frameworkLookup = new[] { moduleHandle == 0 ? 0 : 1, moduleError },
    activationContext = ActivationContextOracle.Probe(moduleHandle), compiledActivation = ActivationContextOracle.ProbeFixture(), compiledLifecycle = ActivationContextProbe.Lifecycle(), compiledPointers = new[] { 0, -1, 123 }
        .SelectMany(value => new[] { ActivationContextProbe.PointerRoundTrip(value, 0), ActivationContextProbe.PointerRoundTrip(value, 1) }),
    nativeLocal = new[] { 0, -1, 123 }.Select(ActivationContextProbe.NativeLocalAlias),
    nativeAggregate = new[] { 0, -1, 123 }.Select(ActivationContextProbe.NativeAggregateAlias),
    windowEnabled = WindowEnabledOracle.Run() }));

[DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true,
    CharSet = CharSet.Unicode, SetLastError = true)]
static extern nint Lookup(string? name);

// Compare observable window state and the synchronous Win32 messages.
static class WindowEnabledOracle
{
    internal static object Run()
    {
        const int disabled = 0x08000000;
        var first = new Window();
        var second = new Window();
        try
        {
            first.CreateHandle(new System.Windows.Forms.CreateParams { ClassName = "STATIC", Width = 1, Height = 1 });
            second.CreateHandle(new System.Windows.Forms.CreateParams { ClassName = "STATIC", Style = disabled, Width = 1, Height = 1 });
            int enabled = WindowEnabledProbe.ReadWindowEnabled(first.Handle);
            int disabledValue = WindowEnabledProbe.ReadWindowEnabled(second.Handle);
            var transitions = new List<object>();
            foreach (int request in new[] { 1, 0, 0, 1, 1 })
            {
                first.Messages.Clear();
                int previous = WindowEnabledProbe.WriteWindowEnabled(first.Handle, request);
                transitions.Add(new { previous, enabled = WindowEnabledProbe.ReadWindowEnabled(first.Handle), messages = first.Messages.ToArray() });
            }
            nint destroyed = first.Handle;
            first.DestroyHandle();
            int[] queries = [WindowEnabledProbe.ReadWindowEnabled(0), enabled, disabledValue, WindowEnabledProbe.ReadWindowEnabled(destroyed)];
            return new { queries, transitions, invalid = WindowEnabledProbe.WriteWindowEnabled(destroyed, 1) };
        }
        finally
        {
            if (first.Handle != 0) first.DestroyHandle();
            if (second.Handle != 0) second.DestroyHandle();
        }
    }

    private sealed class Window : System.Windows.Forms.NativeWindow
    {
        internal readonly List<int[]> Messages = new();
        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == 0x001f || message.Msg == 0x000a)
                Messages.Add([message.Msg, (int)message.WParam, WindowEnabledProbe.ReadWindowEnabled(message.HWnd)]);
            base.WndProc(ref message);
        }
    }
}

// The upstream file-backed ThemingScope passes a module and a numeric
// manifest ID to Windows. Compare handle relationships instead of raw values.
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
