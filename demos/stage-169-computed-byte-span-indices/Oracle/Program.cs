using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ComputedByteSpanIndices;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.AliasedSlice, Probe.SliceBounds, Probe.NegativeIndex,
    Probe.NativePointerIndex, Probe.SideEffectingIndex, Probe.BranchingIndex];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        string rejectedMethod = name == nameof(Probe.SideEffectingIndex) ? nameof(Probe.ReadWithCall) :
            name == nameof(Probe.BranchingIndex) ? nameof(Probe.ReadWithBranch) : null;
        return new
        {
            name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            memory = name == nameof(Probe.NativePointerIndex),
            rejection = rejectedMethod is null ? null : new
            {
                status = -2,
                location = new[] { 0, typeof(Probe).GetMethod(rejectedMethod)!.MetadataToken & 0xffffff, 0 }
            }
        };
    })
}));
