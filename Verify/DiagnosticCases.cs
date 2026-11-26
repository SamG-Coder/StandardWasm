using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using DiagnosticDependency;
using DiagnosticLibrary;

internal static class DiagnosticCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 2048;
        byte[] image = File.ReadAllBytes(typeof(Estimates).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Costs).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] diagnostic = CompilerExports.CreateDiagnostic();
        int[] noLocation = [-1, 0, -1];
        const BindingFlags methodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type estimates = typeof(Estimates);

        var direct = estimates.GetMethod(nameof(Estimates.BoxedValue))!;
        Reject(nameof(Estimates.BoxedValue), image, Row(direct), -2, At(0, direct, OpCodes.Box));
        var entry = estimates.GetMethod(nameof(Estimates.ViaBoxingHelper))!;
        var helper = estimates.GetMethod("BoxCost", methodFlags)!;
        Reject(nameof(Estimates.ViaBoxingHelper), image, Row(entry), -2, At(0, helper, OpCodes.Box));
        var unsupported = estimates.GetMethod(nameof(Estimates.UnsupportedDecimal))!;
        Reject(nameof(Estimates.UnsupportedDecimal), image, Row(unsupported), -2, [0, Row(unsupported), -1]);

        var absolute = estimates.GetMethod(nameof(Estimates.Absolute))!;
        Reject(nameof(Estimates.Absolute), image, Row(absolute), -2, At(0, absolute, OpCodes.Call));
        Reject("invalid metadata", [0], 1, -1, [0, 0, -1]);

        var linked = estimates.GetMethod(nameof(Estimates.ViaBoxedDependency))!;
        var dependencyMethod = typeof(Costs).GetMethod(nameof(Costs.BoxHours))!;
        Reject("dependency opcode", bundle, Manifest(linked), -2, At(1, dependencyMethod, OpCodes.Box), bundled: true);
        Reject("missing dependency", BundleCases.Pack(image), Manifest(linked), -4,
            At(0, linked, OpCodes.Call), bundled: true);
        Reject("invalid dependency metadata", BundleCases.Pack(image, [1]), Manifest(linked), -1,
            [1, 0, -1], bundled: true);
        Reject("invalid bundle", [1], Manifest(linked), -1, noLocation, bundled: true);
        Reject("invalid module manifest", image, new byte[] { 1 }, -1, noLocation, multiExport: true);
        Reject("unsupported method in module", image, Manifest(direct), -2, At(0, direct, OpCodes.Box), multiExport: true);

        var supported = estimates.GetMethod(nameof(Estimates.Supported))!;
        Succeed("single export", image, Row(supported), Estimates.Supported);
        Succeed("module export", image, Manifest(supported), Estimates.Supported, multiExport: true);
        var rate = estimates.GetMethod(nameof(Estimates.LinkedRate))!;
        Succeed("linked export", bundle, Manifest(rate), Estimates.LinkedRate, bundled: true);
        var checkedRate = estimates.GetMethod(nameof(Estimates.ViaDependency))!;
        Succeed("linked checked multiplication", bundle, Manifest(checkedRate), Estimates.ViaDependency, bundled: true);
        var checkedConversion = estimates.GetMethod(nameof(Estimates.ViaUnsupportedDependency))!;
        Succeed("linked checked conversion", bundle, Manifest(checkedConversion), Estimates.ViaUnsupportedDependency, bundled: true);

        Array.Fill(output, (byte)0xa5);
        int[] shortLocation = [17, 29];
        if (CompilerExports.CompileDetailed(image, Row(supported), output, shortLocation) != -3 ||
            !shortLocation.SequenceEqual(new[] { 17, 29 }) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException("A short diagnostic buffer was modified or accepted.");

        void Reject(string name, byte[] file, object selection, int status, int[] location, bool bundled = false, bool multiExport = false)
        {
            CheckFailure(false);
            CheckFailure(true);
            Record(name, file, selection, status, location, [], null, true, bundled, multiExport);

            void CheckFailure(bool detailed)
            {
                Array.Fill(output, (byte)0xa5);
                int actual = Compile(file, selection, detailed, bundled, multiExport);
                if (actual != status || output.Any(value => value != 0xa5) ||
                    detailed && !diagnostic.SequenceEqual(location))
                    throw new InvalidOperationException($"{name}: status {actual}, diagnostic [{string.Join(", ", diagnostic)}]");
            }
        }

        void Succeed(string name, byte[] file, object selection, Func<int, int> evaluate, bool bundled = false, bool multiExport = false)
        {
            int status = Compile(file, selection, false, bundled, multiExport);
            if (status <= 0) throw new InvalidOperationException($"{name}: status {status}");
            byte[] code = output[..status];
            int detailed = Compile(file, selection, true, bundled, multiExport);
            if (detailed != status || !output.AsSpan(0, status).SequenceEqual(code) || !diagnostic.SequenceEqual(noLocation))
                throw new InvalidOperationException($"{name}: diagnostics changed successful output.");
            string hex = Convert.ToHexString(code).ToLowerInvariant();
            foreach (int value in new[] { 0, 7, int.MaxValue })
            {
                int? expected;
                string exception = null;
                try { expected = evaluate(value); }
                catch (OverflowException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                Record(name, file, selection, expected, noLocation, [value], hex, false, bundled, multiExport, exception);
            }
        }

        int Compile(byte[] file, object selection, bool detailed, bool bundled, bool multiExport)
        {
            if (bundled)
                return detailed ? CompilerExports.CompileBundleDetailed(file, (byte[])selection, output, diagnostic)
                    : CompilerExports.CompileBundle(file, (byte[])selection, output);
            if (multiExport)
                return detailed ? CompilerExports.CompileModuleDetailed(file, (byte[])selection, output, diagnostic)
                    : CompilerExports.CompileModule(file, (byte[])selection, output);
            return detailed ? CompilerExports.CompileDetailed(file, (int)selection, output, diagnostic)
                : CompilerExports.Compile(file, (int)selection, output);
        }

        void Record(string fixture, byte[] file, object selection, int? expected, int[] location,
            int[] arguments, string hex, bool reject, bool bundled, bool multiExport, string exception = null) =>
            cases.Add(new
            {
                module = "BootstrapLibrary", method = bundled ? "CompileBundle" : multiExport ? "CompileModule" : "Compile", fixture,
                inputs = new[] { Encode(file), selection is byte[] entries ? Encode(entries) : selection },
                generated = new { capacity, arguments, diagnostic = location, hex, reject, bundle = bundled, multiExport,
                    exceptionType = exception },
                expected, exception
            });
    }

    private static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

    private static int[] At(int module, MethodInfo method, OpCode opcode)
    {
        int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode);
        if (offset < 0) throw new InvalidOperationException($"{method.Name} did not emit {opcode}");
        return [module, Row(method), offset];
    }

    private static byte[] Manifest(MethodInfo method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
