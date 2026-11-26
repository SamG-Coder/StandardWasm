using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using BootstrapLibrary;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: PortableCompiler.Driver <assembly.dll> <full export type name> <module.wasm>");
    return 2;
}

try
{
    byte[] image = File.ReadAllBytes(args[0]);
    using var stream = new MemoryStream(image, writable: false);
    using var pe = new PEReader(stream);
    MetadataReader metadata = pe.GetMetadataReader();
    byte[] manifest = CreateManifest(metadata, args[1]);
    byte[] output = new byte[2 << 20];
    int[] location = CompilerExports.CreateDiagnostic();
    int size = CompilerExports.CompileModuleDetailed(image, manifest, output, location);
    if (size <= 0)
    {
        int row = location[1];
        string method = row > 0 && row <= metadata.MethodDefinitions.Count
            ? metadata.GetString(metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).Name)
            : "<module>";
        string offset = location[2] >= 0 ? $" at IL_{location[2]:x4}" : "";
        Console.Error.WriteLine($"{args[1]}::{method}{offset} returned compiler status {size} (assembly {location[0]}, MethodDef {row}).");
        return 1;
    }

    using var file = File.Create(args[2]);
    file.Write(output, 0, size);
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or BadImageFormatException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static byte[] CreateManifest(MetadataReader metadata, string exportType)
{
    foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
    {
        TypeDefinition type = metadata.GetTypeDefinition(handle);
        if (FullName(metadata, handle) != exportType) continue;

        using var manifest = new MemoryStream();
        using var writer = new BinaryWriter(manifest);
        foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
        {
            MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
            const MethodAttributes mask = MethodAttributes.MemberAccessMask | MethodAttributes.Static;
            if ((method.Attributes & mask) != (MethodAttributes.Public | MethodAttributes.Static)) continue;

            byte[] name = Encoding.UTF8.GetBytes(metadata.GetString(method.Name));
            writer.Write(MetadataTokens.GetRowNumber(methodHandle));
            writer.Write(name.Length);
            writer.Write(name);
        }

        if (manifest.Length == 0) throw new InvalidDataException("No public static exports.");
        return manifest.ToArray();
    }

    throw new InvalidDataException("Export type not found.");
}

static string FullName(MetadataReader metadata, TypeDefinitionHandle handle)
{
    TypeDefinition type = metadata.GetTypeDefinition(handle);
    string name = metadata.GetString(type.Name);
    TypeDefinitionHandle parent = type.GetDeclaringType();
    if (!parent.IsNil) return FullName(metadata, parent) + "/" + name;
    string ns = metadata.GetString(type.Namespace);
    return ns.Length == 0 ? name : ns + "." + name;
}
