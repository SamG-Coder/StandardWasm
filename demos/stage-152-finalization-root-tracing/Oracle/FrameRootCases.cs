using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using ManagedFinalizationExperiment;

internal static class FrameRootCases
{
    private delegate int DecodeBody(byte[] image, int start, int length, int arguments, int locals,
        int[] records, int[] location, bool hasCatches, int first, int[] filterEnds);
    private delegate int AnalyzeFrame(int[] records, int slot, bool hasExceptionHandlers, byte[] live);
    private delegate int DecodeResource(byte[] image, int[] body, int[] records, int[] location, int parameters);

    internal static int Verify(string corePath)
    {
        const BindingFlags internalStatic = BindingFlags.Static | BindingFlags.NonPublic;
        Assembly compiler = typeof(CompilerExports).Assembly;
        var branch = compiler.GetType("PortableCompiler.BranchIl", throwOnError: true)!;
        var decode = branch.GetMethod("Decode", internalStatic)!.CreateDelegate<DecodeBody>();
        int width = (int)branch.GetField("Width", internalStatic)!.GetRawConstantValue()!;
        int call = (int)branch.GetField("Call", internalStatic)!.GetRawConstantValue()!;
        int switchKind = (int)branch.GetField("Switch", internalStatic)!.GetRawConstantValue()!;
        var analyze = compiler.GetType("PortableCompiler.FrameRootLiveness", throwOnError: true)!
            .GetMethod("Analyze", internalStatic)!.CreateDelegate<AnalyzeFrame>();
        Type cases = typeof(Experiment).Assembly.GetType("ManagedFinalizationExperiment.FrameCases", throwOnError: true)!;
        int pause = cases.GetMethod("Pause", internalStatic)!.MetadataToken & 0x00ffffff;
        int checks = 0;

        Check("BeforeAndAfter", 0, true, false);
        Check("ConditionalUse", 0, true, false);
        Check("LoopUse", 0, true, false);
        Check("Replacement", 0, false, true);
        Check("Replacement", 1, true, false);
        Check("SwitchUse", 0, true, false);
        var (switchRecords, _, switchLength) = Read("SwitchUse");
        if (!Enumerable.Range(0, switchLength).Any(pc => switchRecords[pc * width] == switchKind))
            throw new Exception("The switch fixture did not emit a CLI switch");
        Reject("TakeAddress");
        Reject("CatchUse");
        checks += CheckResourcePrelude(corePath, compiler, branch, width, analyze);
        return checks;

        void Check(string name, int slot, params bool[] expected)
        {
            var (records, handlers, length) = Read(name);
            byte[] live = new byte[length];
            if (analyze(records, slot, handlers, live) != 0) throw new Exception($"Root analysis failed for {name}");
            var actual = Enumerable.Range(0, length)
                .Where(pc => records[pc * width] == call && records[pc * width + 1] == pause)
                .Select(pc => live[pc] != 0);
            if (!actual.SequenceEqual(expected))
                throw new Exception($"{name}, frame slot {slot}: {string.Join(", ", actual)}");
            checks++;
        }

        void Reject(string name)
        {
            var (records, handlers, length) = Read(name);
            if (analyze(records, 0, handlers, new byte[length]) != -2)
                throw new Exception($"{name} requires additional root tracking");
            checks++;
        }

        (int[] Records, bool Handlers, int Length) Read(string name)
        {
            MethodInfo method = cases.GetMethod(name, internalStatic)!;
            MethodBody body = method.GetMethodBody()!;
            byte[] il = body.GetILAsByteArray()!;
            bool handlers = body.ExceptionHandlingClauses.Count != 0;
            int[] records = new int[il.Length * width];
            if (decode(il, 0, il.Length, method.GetParameters().Length, body.LocalVariables.Count,
                records, new int[0], handlers, 0, null) != 0)
                throw new Exception($"Could not decode {name}");
            return (records, handlers, il.Length);
        }
    }

    private static int CheckResourcePrelude(string corePath, Assembly compiler, Type branch, int width, AnalyzeFrame analyze)
    {
        const BindingFlags internalStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags internalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        byte[] image = File.ReadAllBytes(corePath);
        Type bundleType = compiler.GetType("PortableCompiler.BundleImages", throwOnError: true)!;
        object bundle = bundleType.GetMethod("Single", internalStatic)!.Invoke(null, [image])!;
        Type planType = compiler.GetType("PortableCompiler.RuntimeReflectionPlan", throwOnError: true)!;
        object plan = Activator.CreateInstance(planType, nonPublic: true)!;
        if ((int)planType.GetMethod("BindResource", internalInstance)!.Invoke(plan, [bundle])! != 0)
            throw new Exception("Could not bind the original CoreLib resource opener");
        int row = (int)planType.GetProperty("ResourceOpen", internalInstance)!.GetValue(plan)!;
        using var pe = new PEReader(new MemoryStream(image, writable: false));
        MetadataReader metadata = pe.GetMetadataReader();
        MethodDefinition definition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        byte[] il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!;
        int[] records = new int[il.Length * width];
        var decode = compiler.GetType("PortableCompiler.ResourceOpenIl", throwOnError: true)!
            .GetMethod("Decode", internalStatic)!.CreateDelegate<DecodeResource>();
        int lookup = (int)branch.GetField("ResourceLookup", internalStatic)!.GetRawConstantValue()!;
        if (decode(il, [0, il.Length], records, [], 2) != 0 || records[0] != lookup)
            throw new Exception("Could not decode the verified resource prelude");
        for (int slot = 0; slot < 2; slot++)
        {
            byte[] live = new byte[il.Length];
            // The original managed tail passes the assembly to the stream constructor.
            // Only the resource name has no further use after the native lookup.
            byte afterLookup = slot == 0 ? (byte)1 : (byte)0;
            if (analyze(records, slot, false, live) != 0 || live[0] != 1 || live[records[2]] != afterLookup)
                throw new Exception($"Unexpected resource prelude liveness for frame slot {slot}");
        }
        return 2;
    }
}
