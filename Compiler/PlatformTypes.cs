using Mono.Cecil;

// Type and member identity for the platform BCL. A name such as System.Int16 or System.Math does not identify a
// type: any assembly can define one, either in the input module or in a dependency reached through an extern alias.
// Cecil also reports MetadataType.Int16 for a third-party TypeRef named System.Int16, so MetadataType alone is not
// enough either. A type counts as a CLI primitive, and a method as a BCL intrinsic, only when its declaring scope is
// a platform BCL assembly: a culture-neutral reference with the assembly name plus its Microsoft public key token.
// A reference with a culture names a different, satellite-style assembly that the CLR does not bind to the BCL (#141).
// That covers the netstandard facade, .NET Framework, the .NET Core contract assemblies and CoreLib. Signature primitives (ELEMENT_TYPE_I2 and so on)
// resolve to the module's core library reference, so they pass the same check.
internal static class PlatformTypes
{
    private static readonly HashSet<(string Name, string Token)> PlatformAssemblies =
    [
        ("netstandard", "cc7b13ffcd2ddd51"),
        ("mscorlib", "b77a5c561934e089"),
        ("System.Runtime", "b03f5f7f11d50a3a"),
        ("System.Runtime.Extensions", "b03f5f7f11d50a3a"),
        ("System.Private.CoreLib", "7cec85d7bea7798e"),
    ];

    internal static bool IsPlatformAssembly(IMetadataScope scope) =>
        scope is AssemblyNameReference assembly && string.IsNullOrEmpty(assembly.Culture) &&
        assembly.PublicKeyToken is { Length: 8 } token &&
        PlatformAssemblies.Contains((assembly.Name, Convert.ToHexString(token).ToLowerInvariant()));

    // The CLI primitive kind of a type, or MetadataType.Class for anything that is not a platform type
    // (including same-named types defined elsewhere), so callers reject it as an unsupported type.
    internal static MetadataType Primitive(TypeReference type) =>
        IsPlatformAssembly(type.Scope) ? type.MetadataType : MetadataType.Class;
}
