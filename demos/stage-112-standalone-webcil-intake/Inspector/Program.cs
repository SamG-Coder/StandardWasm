using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

using var image = new PEReader(File.OpenRead(args.Single()));
var metadata = image.GetMetadataReader();
var token = image.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
var entry = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(token & 0xffffff));
var type = metadata.GetTypeDefinition(entry.GetDeclaringType());
var typeName = metadata.GetString(type.Name);
if (!type.Namespace.IsNil) typeName = metadata.GetString(type.Namespace) + "." + typeName;
Console.WriteLine(JsonSerializer.Serialize(new
{
    assembly = metadata.GetString(metadata.GetAssemblyDefinition().Name),
    entry = typeName + "::" + metadata.GetString(entry.Name),
    token,
    metadataHash = Convert.ToHexString(SHA256.HashData(image.GetMetadata().GetContent().AsSpan()))
}));
