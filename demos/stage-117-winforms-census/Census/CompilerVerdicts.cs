using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using BootstrapLibrary;

namespace WinFormsCensus;

/// <summary>
/// The portable compiler's own result for one export. Location follows
/// CompileLocation: bundle index, one-based MethodDef row, IL offset or -1.
/// </summary>
internal sealed record Verdict(int Status, MethodId? Site, int Offset, double Milliseconds);

/// <summary>
/// Runs the same portable compiler sources that the self-hosted Wasm module is
/// built from (src/PortableCompiler + src/AssemblyReader via the bootstrap
/// library) natively on the CLR. Each reached method is compiled as the only
/// export of a bundle whose main image is the method's own assembly. The
/// compiler lowers the export first and then its callees in registration
/// order, so a failure located in a different method means the export's own
/// body lowered and the first blocker is in its closure.
/// </summary>
internal sealed class CompilerVerdicts
{
    private readonly AssemblySet _set;
    private readonly List<LoadedAssembly> _pack;
    private readonly ConcurrentDictionary<int, byte[]> _bundles = new();
    private static readonly byte[] RunName = Encoding.ASCII.GetBytes("run");

    public CompilerVerdicts(AssemblySet set, List<LoadedAssembly> pack)
    {
        _set = set;
        _pack = pack;
    }

    public IReadOnlyList<LoadedAssembly> Pack => _pack;

    /// <summary>[main, then every other pack assembly in pack order].</summary>
    public List<LoadedAssembly> Order(int mainAssembly)
    {
        var order = new List<LoadedAssembly> { _set.All[mainAssembly] };
        order.AddRange(_pack.Where(a => a.Index != mainAssembly));
        return order;
    }

    private byte[] Bundle(int mainAssembly) => _bundles.GetOrAdd(mainAssembly, main =>
    {
        using var stream = new MemoryStream();
        foreach (LoadedAssembly assembly in Order(main))
        {
            stream.Write(BitConverter.GetBytes(assembly.Bytes.Length));
            stream.Write(assembly.Bytes);
        }
        return stream.ToArray();
    });

    public Verdict Compile(MethodId method)
    {
        var entries = new byte[8 + RunName.Length];
        BitConverter.GetBytes(method.Row).CopyTo(entries, 0);
        BitConverter.GetBytes(RunName.Length).CopyTo(entries, 4);
        RunName.CopyTo(entries, 8);
        byte[] bundle = Bundle(method.Assembly);
        int[] location = CompilerExports.CreateDiagnostic();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int capacity = 256 << 10, status;
        while (true)
        {
            status = CompilerExports.CompileBundleDetailed(bundle, entries, new byte[capacity], location);
            // -3 is a workspace capacity report; the output length sizes the workspace.
            if (status != -3 || capacity >= 64 << 20) break;
            capacity *= 4;
        }
        clock.Stop();
        MethodId? site = null;
        if (status <= 0 && location[0] >= 0 && location[1] > 0)
        {
            List<LoadedAssembly> order = Order(method.Assembly);
            if (location[0] < order.Count) site = new MethodId(order[location[0]].Index, location[1]);
        }
        else if (status <= 0 && location[0] >= 0)
        {
            List<LoadedAssembly> order = Order(method.Assembly);
            if (location[0] < order.Count) site = new MethodId(order[location[0]].Index, 0);
        }
        return new Verdict(status, site, location[2], clock.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// The export boundary rejects interface, value-type and typed-null
    /// parameters and value-type results before lowering. Such a method can
    /// still be judged as a callee of another export, but not as its own root.
    /// </summary>
    public bool ExportShapeRejected(MethodId method)
    {
        MethodDefinition definition = _set.Definition(method);
        LoadedAssembly assembly = _set.All[method.Assembly];
        MethodSignature<string> signature = Signatures.Decode(_set, assembly, definition.Signature);
        if ((definition.Attributes & MethodAttributes.Static) == 0 && _set.TypeOf(method).IsValueType) return true;
        if (IsRejected(signature.ReturnType, true)) return true;
        return signature.ParameterTypes.Any(p => IsRejected(p, false));
    }

    private bool IsRejected(string type, bool result)
    {
        if (type == "Void" || type.EndsWith('&') || type.EndsWith('*')) return false;
        string name = type.Split('<')[0].Split(" modreq")[0];
        if (name.EndsWith("[]")) return false;
        foreach (LoadedAssembly assembly in _set.All)
        {
            int dot = name.LastIndexOf('.');
            string ns = dot < 0 ? "" : name[..dot], simple = dot < 0 ? name : name[(dot + 1)..];
            if (simple.Contains('/')) return false;
            if (!assembly.TopLevelTypes.ContainsKey((ns, simple))) continue;
            TypeId? id = _set.FindType(assembly, ns, simple);
            if (id is null) return false;
            TypeFacts info = _set.Type(id.Value);
            return info.IsValueType || (!result && info.IsInterface);
        }
        // Primitive names (Int32, Boolean, ...) and generic parameters map to
        // compiler scalars or references at the export boundary.
        return false;
    }
}
