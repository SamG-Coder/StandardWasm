using System.Text;
using Mono.Cecil;

internal sealed class ModuleCompiler(ModuleDefinition module)
{
    private readonly Dictionary<MethodDefinition, string> names = new();
    private readonly Queue<MethodDefinition> pending = new();

    internal string Register(MethodDefinition method)
    {
        if (method.Module != module || !method.IsStatic || !method.HasBody || method.HasGenericParameters ||
            method.DeclaringType.HasGenericParameters || method.Body.HasExceptionHandlers ||
            method.DeclaringType.Methods.Any(m => m.Name == ".cctor") || method.IsSynchronized)
            throw new NotSupportedException($"Unsupported method or initialization requirements: {method.FullName}");
        if (!names.TryGetValue(method, out var name))
        {
            name = $"$f{names.Count}";
            names.Add(method, name);
            pending.Enqueue(method);
        }
        return name;
    }

    internal const string Int = "i32";
    // int64 and uint64 share one Wasm type. Signedness lives in the IL instruction (div.un, clt.un, conv.u8),
    // exactly as it does for int32 and uint32.
    internal const string Long = "i64";
    internal const string IntArray = "(ref null $ints)";
    internal const string ByteArray = "(ref null $bytes)";

    internal static string ValueType(TypeReference type) => type.FullName switch
    {
        "System.Int32" or "System.UInt32" or "System.Boolean" or "System.Byte" => Int,
        "System.Int64" or "System.UInt64" => Long,
        "System.Int32[]" => IntArray,
        "System.Byte[]" => ByteArray,
        _ => throw new NotSupportedException($"Unsupported value type: {type.FullName}")
    };

    internal string Compile(TypeDefinition exports)
    {
        if (module.GetType("<Module>")?.Methods.Any(m => m.Name == ".cctor") == true)
            throw new NotSupportedException("Module initializers require managed startup support.");
        var output = new StringBuilder("(module\n (type $ints (array (mut i32)))\n (type $bytes (array (mut i8)))\n");
        var used = new HashSet<string>();
        foreach (var method in exports.Methods.Where(m => m.IsPublic && m.IsStatic))
        {
            if (!used.Add(method.Name)) throw new NotSupportedException($"Ambiguous export: {method.Name}");
            // Byte escapes keep arbitrary CLR metadata names out of WAT syntax.
            var escaped = string.Concat(Encoding.UTF8.GetBytes(method.Name).Select(b => $"\\{b:x2}"));
            output.AppendLine($" (export \"{escaped}\" (func {Register(method)}))");
        }
        if (used.Count == 0) throw new InvalidDataException("No public static exports.");
        while (pending.TryDequeue(out var method))
            output.AppendLine(new MethodCompiler(this, method).Compile(names[method]));
        return output.AppendLine(")").ToString();
    }
}

