using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace WinFormsCensus;

/// <summary>A TypeDef row in one loaded assembly.</summary>
internal readonly record struct TypeId(int Assembly, int Row);

/// <summary>A MethodDef row in one loaded assembly.</summary>
internal readonly record struct MethodId(int Assembly, int Row);

internal sealed class LoadedAssembly
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required byte[] Bytes { get; init; }
    public required MetadataReader Metadata { get; init; }
    public required PEReader Pe { get; init; }
    public readonly Dictionary<(string Namespace, string Name), TypeDefinitionHandle> TopLevelTypes = new();
    public readonly Dictionary<(string Namespace, string Name), ExportedTypeHandle> Forwarders = new();
}

/// <summary>
/// Loads assemblies by simple name from an ordered list of directories (the
/// application publish folder first, then the desktop and shared frameworks)
/// and resolves cross-assembly type and member references. This mirrors the
/// CLR's binding for the purpose of a static census only: version policy and
/// trusted framework binding stay with the linker.
/// </summary>
internal sealed class AssemblySet
{
    private readonly string[] _searchDirectories;
    private readonly Dictionary<string, LoadedAssembly?> _byName = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<LoadedAssembly> All = new();
    private readonly Dictionary<TypeId, TypeFacts> _types = new();

    public AssemblySet(params string[] searchDirectories) => _searchDirectories = searchDirectories;

    public LoadedAssembly Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var pe = new PEReader(ImmutableArray.Create(bytes));
        MetadataReader md = pe.GetMetadataReader();
        var assembly = new LoadedAssembly
        {
            Index = All.Count,
            Name = md.GetString(md.GetAssemblyDefinition().Name),
            Path = path,
            Bytes = bytes,
            Metadata = md,
            Pe = pe,
        };
        foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
        {
            TypeDefinition type = md.GetTypeDefinition(handle);
            if (!type.GetDeclaringType().IsNil) continue;
            assembly.TopLevelTypes[(md.GetString(type.Namespace), md.GetString(type.Name))] = handle;
        }
        foreach (ExportedTypeHandle handle in md.ExportedTypes)
        {
            ExportedType exported = md.GetExportedType(handle);
            assembly.Forwarders[(md.GetString(exported.Namespace), md.GetString(exported.Name))] = handle;
        }
        All.Add(assembly);
        _byName[assembly.Name] = assembly;
        return assembly;
    }

    public LoadedAssembly? ByName(string name)
    {
        if (_byName.TryGetValue(name, out LoadedAssembly? found)) return found;
        foreach (string directory in _searchDirectories)
        {
            string candidate = System.IO.Path.Combine(directory, name + ".dll");
            if (File.Exists(candidate)) return Load(candidate);
        }
        _byName[name] = null;
        return null;
    }

    public readonly SortedSet<string> UnresolvedAssemblies = new(StringComparer.Ordinal);
    public readonly HashSet<LoadedAssembly> ForwardingAssemblies = new();

    private LoadedAssembly? ByReference(LoadedAssembly from, AssemblyReferenceHandle handle)
    {
        string name = from.Metadata.GetString(from.Metadata.GetAssemblyReference(handle).Name);
        LoadedAssembly? target = ByName(name);
        if (target is null) UnresolvedAssemblies.Add(name);
        return target;
    }

    public TypeId? FindType(LoadedAssembly assembly, string ns, string name, int depth = 0)
    {
        if (depth > 8) return null;
        if (assembly.TopLevelTypes.TryGetValue((ns, name), out TypeDefinitionHandle handle))
            return new TypeId(assembly.Index, MetadataTokens.GetRowNumber(handle));
        if (assembly.Forwarders.TryGetValue((ns, name), out ExportedTypeHandle exported))
        {
            EntityHandle implementation = assembly.Metadata.GetExportedType(exported).Implementation;
            if (implementation.Kind == HandleKind.AssemblyReference)
            {
                LoadedAssembly? target = ByReference(assembly, (AssemblyReferenceHandle)implementation);
                TypeId? resolved = target is null ? null : FindType(target, ns, name, depth + 1);
                if (resolved is not null) ForwardingAssemblies.Add(assembly);
                return resolved;
            }
        }
        return null;
    }

    private static TypeId? FindNested(LoadedAssembly assembly, TypeId parent, string name)
    {
        MetadataReader md = assembly.Metadata;
        foreach (TypeDefinitionHandle nested in md.GetTypeDefinition(Handle(parent)).GetNestedTypes())
            if (md.GetString(md.GetTypeDefinition(nested).Name) == name)
                return new TypeId(assembly.Index, MetadataTokens.GetRowNumber(nested));
        return null;
    }

    public TypeId? ResolveTypeReference(LoadedAssembly from, TypeReferenceHandle handle)
    {
        MetadataReader md = from.Metadata;
        TypeReference reference = md.GetTypeReference(handle);
        string ns = md.GetString(reference.Namespace), name = md.GetString(reference.Name);
        EntityHandle scope = reference.ResolutionScope;
        switch (scope.Kind)
        {
            case HandleKind.AssemblyReference:
                LoadedAssembly? target = ByReference(from, (AssemblyReferenceHandle)scope);
                return target is null ? null : FindType(target, ns, name);
            case HandleKind.TypeReference:
                TypeId? parent = ResolveTypeReference(from, (TypeReferenceHandle)scope);
                return parent is null ? null : FindNested(All[parent.Value.Assembly], parent.Value, name);
            case HandleKind.ModuleDefinition:
            case HandleKind.ModuleReference:
                return FindType(from, ns, name);
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves TypeDef, TypeRef and TypeSpec tokens to a TypeDef. A generic
    /// instantiation resolves to its generic definition: the census is
    /// definition-granular, while the compiler specializes bodies per closed
    /// instantiation.
    /// </summary>
    public TypeId? ResolveType(LoadedAssembly from, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return new TypeId(from.Index, MetadataTokens.GetRowNumber(handle));
            case HandleKind.TypeReference:
                return ResolveTypeReference(from, (TypeReferenceHandle)handle);
            case HandleKind.TypeSpecification:
                BlobReader blob = from.Metadata.GetBlobReader(from.Metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
                return ResolveTypeSpecBlob(from, ref blob);
            default:
                return null;
        }
    }

    private TypeId? ResolveTypeSpecBlob(LoadedAssembly from, ref BlobReader blob)
    {
        SignatureTypeCode code = blob.ReadSignatureTypeCode();
        switch (code)
        {
            case SignatureTypeCode.GenericTypeInstance:
                blob.ReadSignatureTypeCode(); // CLASS or VALUETYPE
                return ResolveType(from, blob.ReadTypeHandle());
            case SignatureTypeCode.TypeHandle:
                return ResolveType(from, blob.ReadTypeHandle());
            default:
                return null; // arrays, pointers, generic parameters
        }
    }

    public static TypeDefinitionHandle Handle(TypeId id) => MetadataTokens.TypeDefinitionHandle(id.Row);
    public static MethodDefinitionHandle Handle(MethodId id) => MetadataTokens.MethodDefinitionHandle(id.Row);

    public TypeFacts Type(TypeId id)
    {
        if (_types.TryGetValue(id, out TypeFacts? info)) return info;
        info = new TypeFacts(this, id);
        _types[id] = info;
        info.Initialize();
        return info;
    }

    public TypeFacts TypeOf(MethodId method)
    {
        LoadedAssembly assembly = All[method.Assembly];
        TypeDefinitionHandle owner = assembly.Metadata.GetMethodDefinition(Handle(method)).GetDeclaringType();
        return Type(new TypeId(method.Assembly, MetadataTokens.GetRowNumber(owner)));
    }

    /// <summary>
    /// Resolves a MethodDef, MemberRef or MethodSpec token to a MethodDef.
    /// MethodSpec resolves to its generic method definition.
    /// </summary>
    public MethodId? ResolveMethod(LoadedAssembly from, EntityHandle handle, out bool viaMethodSpec)
    {
        viaMethodSpec = false;
        MetadataReader md = from.Metadata;
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                return new MethodId(from.Index, MetadataTokens.GetRowNumber(handle));
            case HandleKind.MethodSpecification:
                viaMethodSpec = true;
                return ResolveMethod(from, md.GetMethodSpecification((MethodSpecificationHandle)handle).Method, out _);
            case HandleKind.MemberReference:
                MemberReference reference = md.GetMemberReference((MemberReferenceHandle)handle);
                if (reference.GetKind() != MemberReferenceKind.Method) return null;
                if (reference.Parent.Kind == HandleKind.MethodDefinition)
                    return new MethodId(from.Index, MetadataTokens.GetRowNumber(reference.Parent));
                TypeId? parent = ResolveType(from, reference.Parent);
                if (parent is null) return null;
                string name = md.GetString(reference.Name);
                string signature = Signatures.Method(this, from, reference.Signature);
                return FindMethod(parent.Value, name, signature);
            default:
                return null;
        }
    }

    public MethodId? FindMethod(TypeId start, string name, string signature)
    {
        // A MemberRef may name an inherited member through a derived parent.
        for (TypeId? current = start; current is not null; current = Type(current.Value).BaseType)
        {
            TypeFacts type = Type(current.Value);
            MethodId? loose = null;
            int looseCount = 0;
            foreach (MethodId method in type.Methods)
            {
                if (MethodName(method) != name) continue;
                if (MethodSignature(method) == signature) return method;
                if (Signatures.SameShape(MethodSignature(method), signature)) { loose = method; looseCount++; }
            }
            if (looseCount == 1) return loose;
        }
        return null;
    }

    /// <summary>Resolves a field token to its declaring TypeDef.</summary>
    public (TypeId? Owner, string Name) ResolveFieldOwner(LoadedAssembly from, EntityHandle handle)
    {
        MetadataReader md = from.Metadata;
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            FieldDefinition field = md.GetFieldDefinition((FieldDefinitionHandle)handle);
            return (new TypeId(from.Index, MetadataTokens.GetRowNumber(field.GetDeclaringType())), md.GetString(field.Name));
        }
        if (handle.Kind == HandleKind.MemberReference)
        {
            MemberReference reference = md.GetMemberReference((MemberReferenceHandle)handle);
            string name = md.GetString(reference.Name);
            TypeId? owner = ResolveType(from, reference.Parent);
            // Walk to the TypeDef that declares the named field.
            for (TypeId? current = owner; current is not null; current = Type(current.Value).BaseType)
                if (Type(current.Value).FieldNames.Contains(name)) return (current, name);
            return (owner, name);
        }
        return (null, "");
    }

    private readonly Dictionary<MethodId, string> _signatures = new();
    public string MethodSignature(MethodId id)
    {
        if (_signatures.TryGetValue(id, out string? s)) return s;
        LoadedAssembly assembly = All[id.Assembly];
        s = Signatures.Method(this, assembly, assembly.Metadata.GetMethodDefinition(Handle(id)).Signature);
        _signatures[id] = s;
        return s;
    }

    public string MethodName(MethodId id)
    {
        MetadataReader md = All[id.Assembly].Metadata;
        return md.GetString(md.GetMethodDefinition(Handle(id)).Name);
    }

    public MethodDefinition Definition(MethodId id) => All[id.Assembly].Metadata.GetMethodDefinition(Handle(id));

    public string Display(MethodId id) => Type(TypeOf(id).Id).FullName + "::" + MethodName(id);
}

/// <summary>Cached per-TypeDef facts used by the call graph and the classifier.</summary>
internal sealed class TypeFacts
{
    private readonly AssemblySet _set;
    public TypeId Id { get; }
    public string FullName { get; private set; } = "";
    public TypeAttributes Attributes { get; private set; }
    public TypeId? BaseType { get; private set; }
    public readonly List<TypeId> Interfaces = new();
    public readonly List<MethodId> Methods = new();
    public readonly HashSet<string> FieldNames = new(StringComparer.Ordinal);
    public MethodId? StaticConstructor { get; private set; }
    public bool IsInterface => (Attributes & TypeAttributes.Interface) != 0;
    public bool IsExplicitLayout => (Attributes & TypeAttributes.LayoutMask) == TypeAttributes.ExplicitLayout;
    public bool IsBeforeFieldInit => (Attributes & TypeAttributes.BeforeFieldInit) != 0;
    public bool IsGenericDefinition { get; private set; }
    public bool IsValueType { get; private set; }
    public bool IsDelegate { get; private set; }
    public bool IsComImport => (Attributes & TypeAttributes.Import) != 0;
    /// <summary>Declaration -> body MethodImpl pairs declared on this type.</summary>
    public readonly List<(EntityHandle Declaration, MethodId Body)> MethodImpls = new();
    private HashSet<TypeId>? _ancestors;

    public TypeFacts(AssemblySet set, TypeId id)
    {
        _set = set;
        Id = id;
    }

    internal void Initialize()
    {
        LoadedAssembly assembly = _set.All[Id.Assembly];
        MetadataReader md = assembly.Metadata;
        TypeDefinition type = md.GetTypeDefinition(AssemblySet.Handle(Id));
        Attributes = type.Attributes;
        string name = md.GetString(type.Name);
        TypeDefinitionHandle declaring = type.GetDeclaringType();
        FullName = !declaring.IsNil
            ? _set.Type(new TypeId(Id.Assembly, MetadataTokens.GetRowNumber(declaring))).FullName + "/" + name
            : (type.Namespace.IsNil || md.GetString(type.Namespace).Length == 0 ? name : md.GetString(type.Namespace) + "." + name);
        IsGenericDefinition = type.GetGenericParameters().Count > 0;
        if (!type.BaseType.IsNil) BaseType = _set.ResolveType(assembly, type.BaseType);
        foreach (InterfaceImplementationHandle handle in type.GetInterfaceImplementations())
        {
            TypeId? implemented = _set.ResolveType(assembly, md.GetInterfaceImplementation(handle).Interface);
            if (implemented is not null) Interfaces.Add(implemented.Value);
        }
        foreach (MethodDefinitionHandle handle in type.GetMethods())
        {
            var method = new MethodId(Id.Assembly, MetadataTokens.GetRowNumber(handle));
            Methods.Add(method);
            MethodDefinition definition = md.GetMethodDefinition(handle);
            if ((definition.Attributes & MethodAttributes.Static) != 0 && md.GetString(definition.Name) == ".cctor")
                StaticConstructor = method;
        }
        foreach (FieldDefinitionHandle handle in type.GetFields())
            FieldNames.Add(md.GetString(md.GetFieldDefinition(handle).Name));
        foreach (MethodImplementationHandle handle in type.GetMethodImplementations())
        {
            MethodImplementation impl = md.GetMethodImplementation(handle);
            MethodId? body = _set.ResolveMethod(assembly, impl.MethodBody, out _);
            if (body is not null) MethodImpls.Add((impl.MethodDeclaration, body.Value));
        }
        if (BaseType is not null)
        {
            string baseName = _set.Type(BaseType.Value).FullName;
            IsValueType = (baseName == "System.ValueType" || baseName == "System.Enum") && FullName != "System.Enum";
            IsDelegate = baseName == "System.MulticastDelegate";
        }
    }

    /// <summary>All base classes and (transitively) implemented interfaces, including this type.</summary>
    public HashSet<TypeId> Ancestors
    {
        get
        {
            if (_ancestors is not null) return _ancestors;
            var result = new HashSet<TypeId> { Id };
            if (BaseType is not null) result.UnionWith(_set.Type(BaseType.Value).Ancestors);
            foreach (TypeId implemented in Interfaces) result.UnionWith(_set.Type(implemented).Ancestors);
            _ancestors = result;
            return result;
        }
    }
}
