using System.Text;
using Mono.Cecil;

internal sealed class ModuleCompiler(ModuleDefinition module)
{
    private readonly Dictionary<MethodDefinition, string> names = new();
    private readonly Queue<MethodDefinition> pending = new();
    // Runtime helpers (RuntimeHelpers.Functions) this module references, emitted once after the methods.
    private readonly SortedSet<string> helpers = new(StringComparer.Ordinal);

    internal string RequireHelper(string name)
    {
        if (!RuntimeHelpers.Functions.ContainsKey(name)) throw new InvalidOperationException($"Unknown runtime helper {name}");
        helpers.Add(name);
        return "$" + name;
    }

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
    // float32 and float64 stay distinct Wasm types so float arithmetic rounds to single precision like the CLR.
    internal const string Float32 = "f32";
    internal const string Float64 = "f64";
    internal static readonly string IntArray = ArrayKinds.Ints.Reference;
    internal static readonly string ByteArray = ArrayKinds.Bytes.Reference;

    // Small integer types live in i32 like on the CLR evaluation stack. MethodCompiler.Normalize restores their
    // range wherever a value enters or leaves a typed location (argument, local, return).
    // Primitives are classified by CLI identity (PlatformTypes), never by name, so a third-party type called
    // System.Int16 is rejected instead of being lowered as a 16-bit integer.
    internal static string ValueType(TypeReference type) => PlatformTypes.Primitive(type) switch
    {
        MetadataType.Int32 or MetadataType.UInt32 or MetadataType.Boolean or MetadataType.Byte or MetadataType.SByte or
        MetadataType.Int16 or MetadataType.UInt16 or MetadataType.Char => Int,
        MetadataType.Int64 or MetadataType.UInt64 => Long,
        MetadataType.Single => Float32,
        MetadataType.Double => Float64,
        _ when type is ArrayType { IsVector: true } array && ArrayKinds.ForElement(array.ElementType) is { } kind => kind.Reference,
        _ => throw new NotSupportedException($"Unsupported value type: {type.FullName} in {type.Scope?.Name}")
    };

    internal string Compile(TypeDefinition exports)
    {
        if (module.GetType("<Module>")?.Methods.Any(m => m.Name == ".cctor") == true)
            throw new NotSupportedException("Module initializers require managed startup support.");
        var output = new StringBuilder("(module\n");
        foreach (var kind in ArrayKinds.All) output.AppendLine(kind.Declaration);
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
        foreach (var helper in helpers)
            output.AppendLine(RuntimeHelpers.Functions[helper]);
        return output.AppendLine(")").ToString();
    }
}

