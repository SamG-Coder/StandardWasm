using System.Reflection.Emit;
using BootstrapLibrary;
using PlatformIdentityProbe;

internal static class PlatformIdentityCases
{
    internal static void Add(List<object> cases)
    {
        try
        {
            Probe.ObjectBase(7);
            throw new InvalidOperationException("Expected the third-party base constructor to run.");
        }
        catch (InvalidOperationException error) when (error.Message == "Third-party base constructor")
        {
        }
        try
        {
            Probe.InvokeEvent();
            throw new InvalidOperationException("Expected a null delegate call on the CLR.");
        }
        catch (NullReferenceException)
        {
        }

        if (Probe.Concat() != 5 || Probe.InvokeFunc() != 29 || Probe.EventArgsEmpty() != 1 ||
            Probe.Exchange() != 0 || Probe.Combine() != 1)
            throw new InvalidOperationException("Unexpected CLR result from a third-party type.");

        const int capacity = 16384;
        var type = typeof(Probe);
        var image = File.ReadAllBytes(type.Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        var output = new byte[capacity];
        var diagnostic = CompilerExports.CreateDiagnostic();

        Reject(nameof(Probe.ObjectBase), OpCodes.Newobj);
        Reject(nameof(Probe.Concat), OpCodes.Call);
        Reject(nameof(Probe.InvokeFunc), OpCodes.Ldftn);
        Reject(nameof(Probe.EventArgsEmpty), OpCodes.Ldsfld);
        Reject(nameof(Probe.InvokeEvent), OpCodes.Callvirt);
        Reject(nameof(Probe.Exchange), OpCodes.Ldflda);
        Reject(nameof(Probe.Combine), OpCodes.Call);

        void Reject(string name, OpCode rejectedOpcode)
        {
            var method = type.GetMethod(name)!;
            int row = method.MetadataToken & 0xffffff;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, rejectedOpcode);
            if (offset < 0) throw new InvalidOperationException($"{name} lacks {rejectedOpcode}.");
            int[] location = [0, row, offset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileDetailed(image, row, output, diagnostic);
            if (status != -2 || !diagnostic.SequenceEqual(location) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name}: status {status}, location [{string.Join(", ", diagnostic)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = "third-party type: " + name,
                inputs = new object[] { encoded, row },
                generated = new { capacity, arguments = Array.Empty<int>(), diagnostic = location, reject = true },
                expected = status
            });
        }
    }
}
