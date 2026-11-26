using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;

namespace WinFormsCensus;

/// <summary>
/// Explains method-level compiler failures (IL offset -1), where the compiler
/// reports no instruction. It is differential evidence, not the compiler's
/// reason. Each method contributes the type texts of its return, parameters,
/// locals and declaring type. A type that occurs in failed methods but in no
/// method whose own body got past method level is a suspect. Static feature
/// tags take part in the same comparison. Each failure is
/// attributed to its most frequent suspect.
/// </summary>
internal sealed class MethodLevelEvidence
{
    private readonly AssemblySet _set;
    private readonly Dictionary<string, int> _failed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _passed = new(StringComparer.Ordinal);
    private readonly Dictionary<MethodId, string[]> _atoms = new();
    private readonly Dictionary<string, TypeFacts?> _resolved = new(StringComparer.Ordinal);
    // MethodCompiler rejects a method with locals whose header clears
    // CorILMethod_InitLocals before reading its IL; [SkipLocalsInit] code does.
    public const string SkipLocalsInit = "feature: locals without localsinit ([SkipLocalsInit])";

    private readonly IReadOnlyDictionary<MethodId, SortedSet<string>> _tags;

    public MethodLevelEvidence(AssemblySet set, IReadOnlyDictionary<MethodId, Verdict> results, IReadOnlyDictionary<MethodId, SortedSet<string>> tags)
    {
        _set = set;
        _tags = tags;
        foreach ((MethodId method, Verdict verdict) in results)
        {
            bool failedHere = verdict.Status <= 0 && verdict.Site == method && verdict.Offset < 0;
            // The whole body lowered and emitted: the method compiled or failed in a
            // callee. An instruction-offset failure stops before stack analysis and
            // emission, so it is no evidence that those stages accept its features.
            bool passedHere = verdict.Status > 0 || verdict.Site is { } site && site != method;
            if (!failedHere && !passedHere) continue;
            string[] atoms = Atoms(method);
            _atoms[method] = atoms;
            Dictionary<string, int> counts = failedHere ? _failed : _passed;
            foreach (string atom in atoms) counts[atom] = counts.GetValueOrDefault(atom) + 1;
        }
    }

    private string[] Atoms(MethodId method)
    {
        LoadedAssembly assembly = _set.All[method.Assembly];
        MethodDefinition definition = _set.Definition(method);
        var atoms = new SortedSet<string>(StringComparer.Ordinal) { "declaring " + _set.TypeOf(method).FullName };
        MethodSignature<string> signature = Signatures.Decode(_set, assembly, definition.Signature);
        atoms.Add(signature.ReturnType);
        foreach (string parameter in signature.ParameterTypes) atoms.Add(parameter);
        if (definition.RelativeVirtualAddress != 0)
        {
            MethodBodyBlock body = assembly.Pe.GetMethodBody(definition.RelativeVirtualAddress);
            if (!body.LocalSignature.IsNil)
                foreach (string local in Signatures.Locals(_set, assembly, body.LocalSignature)) atoms.Add(local);
        }
        atoms.Remove("Void");
        // Offset -1 also covers stack analysis and emission, which run after the
        // compiler resets the IL location. Static feature tags separate those.
        if (_tags.TryGetValue(method, out SortedSet<string>? features))
            foreach (string feature in features) atoms.Add("feature: " + feature);
        return atoms.ToArray();
    }

    /// <summary>The suspect type and its kind, or null when no type distinguishes the failure.</summary>
    public (string Kind, string Type)? Explain(MethodId method)
    {
        if (!_atoms.TryGetValue(method, out string[]? atoms)) return null;
        // A header flag that never occurs in a passing method explains the failure
        // before any type does.
        if (atoms.Contains(SkipLocalsInit) && !_passed.ContainsKey(SkipLocalsInit)) return ("locals without localsinit ([SkipLocalsInit])", "");
        string? best = null;
        foreach (string atom in atoms)
        {
            if (_passed.ContainsKey(atom)) continue;
            if (best is null || _failed[atom] > _failed[best] || _failed[atom] == _failed[best] && string.CompareOrdinal(atom, best) < 0)
                best = atom;
        }
        return best is null ? null : (Kind(best), best);
    }

    public IEnumerable<object> TopSuspects(int count) => _failed
        .Where(p => !_passed.ContainsKey(p.Key))
        .OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(count)
        .Select(p => new { type = p.Key, kind = Kind(p.Key), failedMethods = p.Value });

    private string Kind(string atom)
    {
        if (atom.StartsWith("feature: ", StringComparison.Ordinal)) return atom["feature: ".Length..];
        if (atom.StartsWith("declaring ", StringComparison.Ordinal))
        {
            TypeFacts? owner = Resolve(atom["declaring ".Length..]);
            if (owner is null) return "declaring type";
            if (owner.IsGenericDefinition) return "declaring generic type";
            if (owner.IsExplicitLayout) return "declaring explicit-layout type";
            if (owner.IsValueType) return "declaring value type";
            if (owner.IsInterface) return "declaring interface";
            return "declaring reference type";
        }
        if (atom.Contains(" pinned")) return "pinned local";
        if (atom.Contains("method ")) return "function pointer";
        if (atom.Contains('*')) return "unmanaged pointer";
        if (atom.StartsWith("System.Span`1<", StringComparison.Ordinal) || atom.StartsWith("System.ReadOnlySpan`1<", StringComparison.Ordinal))
            return "Span<T> / ReadOnlySpan<T>";
        if (atom.EndsWith('&')) return "managed byref";
        if (atom.Contains('<')) return Resolve(atom.Split('<')[0]) is { IsValueType: true } ? "generic value-type instantiation" : "generic reference-type instantiation";
        if (atom.Contains("!!") || atom.StartsWith('!')) return "generic parameter";
        if (atom.EndsWith("]", StringComparison.Ordinal)) return "array type";
        TypeFacts? type = Resolve(atom.Split(" modreq")[0]);
        if (type is null) return atom is "IntPtr" or "UIntPtr" ? "native integer" : "primitive " + atom;
        if (type.IsExplicitLayout) return "explicit-layout value type";
        if (type.IsValueType) return _set.Type(type.BaseType!.Value).FullName == "System.Enum" ? "enum" : "value type";
        if (type.IsDelegate) return "delegate type";
        if (type.IsInterface) return "interface type";
        return "reference type";
    }

    private TypeFacts? Resolve(string name)
    {
        if (_resolved.TryGetValue(name, out TypeFacts? cached)) return cached;
        TypeFacts? found = null;
        string[] nested = name.Split('/');
        int dot = nested[0].LastIndexOf('.');
        string ns = dot < 0 ? "" : nested[0][..dot], simple = dot < 0 ? nested[0] : nested[0][(dot + 1)..];
        foreach (LoadedAssembly assembly in _set.All.ToArray())
        {
            if (!assembly.TopLevelTypes.ContainsKey((ns, simple))) continue;
            TypeId? id = _set.FindType(assembly, ns, simple);
            if (id is null) continue;
            found = _set.Type(id.Value);
            for (int i = 1; i < nested.Length && found is not null; i++)
            {
                MetadataReader md = _set.All[found.Id.Assembly].Metadata;
                TypeFacts? next = null;
                foreach (TypeDefinitionHandle child in md.GetTypeDefinition(AssemblySet.Handle(found.Id)).GetNestedTypes())
                    if (md.GetString(md.GetTypeDefinition(child).Name) == nested[i])
                        next = _set.Type(new TypeId(found.Id.Assembly, System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(child)));
                found = next;
            }
            break;
        }
        _resolved[name] = found;
        return found;
    }
}
