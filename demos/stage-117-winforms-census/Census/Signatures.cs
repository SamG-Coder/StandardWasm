using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace WinFormsCensus;

/// <summary>
/// Canonical, assembly-independent signature text used to match MemberRefs
/// and overrides to MethodDefs. Type names are the referenced namespace and
/// name (unchanged by type forwarding). Optional modifiers are dropped;
/// required modifiers are kept because they distinguish overloads.
/// </summary>
internal static class Signatures
{
    public static string Method(AssemblySet set, LoadedAssembly assembly, BlobHandle blob)
    {
        var decoder = new SignatureDecoder<string, object?>(new NameProvider(set, assembly), assembly.Metadata, null);
        BlobReader reader = assembly.Metadata.GetBlobReader(blob);
        MethodSignature<string> signature = decoder.DecodeMethodSignature(ref reader);
        return $"{signature.GenericParameterCount}|{signature.ReturnType}({string.Join(",", signature.ParameterTypes)})";
    }

    public static ImmutableArray<string> Locals(AssemblySet set, LoadedAssembly assembly, StandaloneSignatureHandle handle)
    {
        var decoder = new SignatureDecoder<string, object?>(new NameProvider(set, assembly), assembly.Metadata, null);
        BlobReader reader = assembly.Metadata.GetBlobReader(assembly.Metadata.GetStandaloneSignature(handle).Signature);
        return decoder.DecodeLocalSignature(ref reader);
    }

    public static MethodSignature<string> Decode(AssemblySet set, LoadedAssembly assembly, BlobHandle blob)
    {
        var decoder = new SignatureDecoder<string, object?>(new NameProvider(set, assembly), assembly.Metadata, null);
        BlobReader reader = assembly.Metadata.GetBlobReader(blob);
        return decoder.DecodeMethodSignature(ref reader);
    }

    /// <summary>
    /// Same generic arity and parameter count. Used only as a unique-candidate
    /// fallback when generic substitution changes the textual signature (for
    /// example an override of Base&lt;T&gt;.M(T) declared as M(int)).
    /// </summary>
    public static bool SameShape(string a, string b)
    {
        int ga = a.IndexOf('|'), gb = b.IndexOf('|');
        if (a[..ga] != b[..gb]) return false;
        return ParameterCount(a) == ParameterCount(b);
    }

    private static int ParameterCount(string signature)
    {
        int open = signature.IndexOf('('), depth = 0, count = 0;
        bool any = false;
        for (int i = open + 1; i < signature.Length - 1; i++)
        {
            char c = signature[i];
            if (c == '<' || c == '[' || c == '(') depth++;
            else if (c == '>' || c == ']' || c == ')') depth--;
            else if (c == ',' && depth == 0) count++;
            any = true;
        }
        return any ? count + 1 : 0;
    }

    private sealed class NameProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly AssemblySet _set;
        private readonly LoadedAssembly _assembly;
        public NameProvider(AssemblySet set, LoadedAssembly assembly) { _set = set; _assembly = assembly; }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            _set.Type(new TypeId(_assembly.Index, MetadataTokens.GetRowNumber(handle))).FullName;
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference reference = reader.GetTypeReference(handle);
            string name = reader.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
                return GetTypeFromReference(reader, (TypeReferenceHandle)reference.ResolutionScope, 0) + "/" + name;
            string ns = reader.GetString(reference.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPinnedType(string elementType) => elementType + " pinned";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            isRequired ? unmodifiedType + " modreq(" + modifier + ")" : unmodifiedType;
        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            "method " + signature.ReturnType + "*(" + string.Join(",", signature.ParameterTypes) + ")";
    }
}
