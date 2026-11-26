using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

internal static class PortableAuditRows
{
    private sealed record Request(string Assembly, string Dll, string ExportType, string[] Names);

    internal static void Write(string request, string output)
    {
        var fixtures = JsonSerializer.Deserialize<Request[]>(File.ReadAllText(request))
            ?? throw new InvalidDataException("Missing fixture export requests");
        var result = new Dictionary<string, Dictionary<string, int>>();
        foreach (var fixture in fixtures)
        {
            using var stream = File.OpenRead(fixture.Dll);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var definition = metadata.TypeDefinitions.Single(handle => FullName(metadata, handle) == fixture.ExportType);
            var available = metadata.GetTypeDefinition(definition).GetMethods()
                .Where(handle =>
                {
                    var flags = metadata.GetMethodDefinition(handle).Attributes;
                    return (flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Public &&
                        (flags & MethodAttributes.Static) != 0;
                })
                .ToLookup(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name), StringComparer.Ordinal);
            var selected = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var name in fixture.Names)
            {
                var handles = available[name];
                if (handles.Count() != 1)
                    throw new InvalidDataException($"{fixture.ExportType}.{name} does not identify a unique public static export");
                selected.Add(name, MetadataTokens.GetRowNumber(handles.Single()));
            }
            result.Add(fixture.Assembly, selected);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(result));
    }

    private static string FullName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var definition = metadata.GetTypeDefinition(handle);
        string name = metadata.GetString(definition.Name);
        var parent = definition.GetDeclaringType();
        if (!parent.IsNil) return FullName(metadata, parent) + "/" + name;
        string ns = metadata.GetString(definition.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
}
