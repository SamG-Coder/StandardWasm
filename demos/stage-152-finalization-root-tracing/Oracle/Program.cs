using System.Runtime.CompilerServices;
using System.Text.Json;
using ManagedFinalizationExperiment;

int[] expected = [23, 0, 160, 0, 58, 63, 140];
int[] result = Enumerable.Range(0, expected.Length).Select(Experiment.Run).ToArray();
if (!result.SequenceEqual(expected)) throw new Exception($"Model: {string.Join(", ", result)}");
for (int scenario = 0; scenario < 3; scenario++)
{
    RealReceiver.Result = 0;
    RealReceiver.Resurrected = null;
    RealReceiver keep = Arrange(scenario);
    Collect();
    int value = RealReceiver.Result;
    GC.KeepAlive(keep);
    if (scenario == 1)
    {
        GC.SuppressFinalize(keep.Child);
    }
    if (scenario == 2)
    {
        RealReceiver.Resurrected.Child.Child = null;
        RealReceiver.Resurrected.Child.Value += 3;
        Collect();
        if (RealReceiver.Result != value) throw new Exception("Resurrected object finalized twice");
        value += RealReceiver.Resurrected.Child.Value;
    }
    if (value != result[scenario]) throw new Exception($"Actual CLR case {scenario}: {value}");
}
var row = typeof(Experiment).GetMethod(nameof(Experiment.Run))!.MetadataToken & 0x00ffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    row,
    result,
    runtime = Environment.Version.ToString(),
    nativeBytes = IntPtr.Size,
    actualClrCases = 3,
    frameRootCases = FrameRootCases.Verify(args[1]),
    frameworkEventTypes = FrameworkEventCases.Verify(args[1]),
    rootProbeResult = EmitRootProbe.Write(Path.Combine(Path.GetDirectoryName(args[0])!, "root-probe.wasm"), args[1]),
    graph = EmitRootProbe.DescribeGraph(),
    allocationFinalizers = EmitRootProbe.ObserveAllocations(),
    suppression = SuppressionCases.Verify(),
    pin = EmitRootProbe.ObservePinned(),
    protectedExceptions = EmitRootProbe.ObserveProtectedExceptions(),
    initializerResults = EmitRootProbe.ObserveInitializers(),
}));

[MethodImpl(MethodImplOptions.NoInlining)]
static RealReceiver Arrange(int scenario)
{
    var first = new RealReceiver { Value = 17, Resurrect = scenario == 2 };
    var second = new RealReceiver { Value = 23 };
    var child = new RealReceiver { Value = 41, Child = first };
    GC.SuppressFinalize(child);
    first.Child = child;
    if (scenario != 0) GC.SuppressFinalize(second);
    if (scenario != 1)
    {
        GC.SuppressFinalize(first);
        GC.SuppressFinalize(first);
    }
    if (scenario == 2)
    {
        GC.ReRegisterForFinalize(first);
        GC.ReRegisterForFinalize(first);
    }
    return scenario == 1 ? child : null;
}

static void Collect()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
}

sealed class RealReceiver
{
    internal static RealReceiver Resurrected;
    internal static int Result;
    internal RealReceiver Child;
    internal int Value;
    internal bool Resurrect;

    ~RealReceiver()
    {
        Interlocked.Add(ref Result, Value + (Child?.Value ?? 0));
        if (Resurrect) Resurrected = this;
    }
}
