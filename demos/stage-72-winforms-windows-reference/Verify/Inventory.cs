using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

internal static class Inventory
{
    internal static object Read(string entry)
    {
        var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.GetFiles(Path.GetDirectoryName(entry)!, "*.dll"))
            available.TryAdd(Path.GetFileNameWithoutExtension(file), file);
        foreach (string file in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator))
            if (file.Length != 0) available.TryAdd(Path.GetFileNameWithoutExtension(file), file);

        var pending = new Queue<string>();
        var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var assemblies = new List<object>();
        pending.Enqueue(entry);
        while (pending.TryDequeue(out string? path))
        {
            if (!scanned.Add(path)) continue;
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            if (!image.HasMetadata) throw new InvalidOperationException("Expected managed metadata in " + path);
            MetadataReader metadata = image.GetMetadataReader();
            var references = new List<object>();
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                AssemblyName name = reference.GetAssemblyName();
                string? candidate = name.Name is null ? null : available.GetValueOrDefault(name.Name);
                references.Add(new { identity = name.FullName, found = candidate != null });
                if (candidate != null) pending.Enqueue(candidate);
                else unresolved.Add(name.FullName);
            }
            var forwards = new List<object>();
            foreach (ExportedTypeHandle handle in metadata.ExportedTypes)
            {
                ExportedType type = metadata.GetExportedType(handle);
                if (!type.IsForwarder) continue;
                string? target = type.Implementation.Kind == HandleKind.AssemblyReference
                    ? metadata.GetAssemblyReference((AssemblyReferenceHandle)type.Implementation).GetAssemblyName().FullName
                    : null;
                forwards.Add(new { name = metadata.GetString(type.Name), @namespace = metadata.GetString(type.Namespace), target });
            }
            var imports = new List<object>();
            foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
            {
                MethodDefinition method = metadata.GetMethodDefinition(handle);
                if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
                MethodImport import = method.GetImport();
                var type = metadata.GetTypeDefinition(method.GetDeclaringType());
                imports.Add(new
                {
                    type = metadata.GetString(type.Name),
                    @namespace = metadata.GetString(type.Namespace),
                    method = metadata.GetString(method.Name),
                    module = metadata.GetString(metadata.GetModuleReference(import.Module).Name),
                    entryPoint = metadata.GetString(import.Name)
                });
            }
            assemblies.Add(new
            {
                identity = metadata.GetAssemblyDefinition().GetAssemblyName().FullName,
                file = Path.GetFileName(path),
                sha256 = Hash(path),
                references,
                forwards,
                resources = metadata.ManifestResources.Select(h => metadata.GetString(metadata.GetManifestResource(h).Name)).ToArray(),
                imports
            });
        }
        return new
        {
            mode = "static metadata inventory; dependency candidates are by simple name, not runtime binding",
            assemblies,
            unresolved
        };
    }

    internal static object[] Files(string dir) => Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
        .OrderBy(file => file, StringComparer.Ordinal)
        .Select(file => (object)new { path = Path.GetRelativePath(dir, file), length = new FileInfo(file).Length, sha256 = Hash(file) })
        .ToArray();

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
