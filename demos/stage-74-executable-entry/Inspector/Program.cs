using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

using var image = new PEReader(File.OpenRead(args.Single()));
var metadata = image.GetMetadataReader();
var token = image.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
var entry = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(token & 0xffffff));
var instructions = image.GetMethodBody(entry.RelativeVirtualAddress).GetILBytes()!;
var declaring = metadata.GetTypeDefinition(entry.GetDeclaringType());

for (int offset = 0; offset < instructions.Length;)
{
    byte opcode = instructions[offset];
    if (opcode is 0 or 0x2a) { offset++; continue; }
    if (opcode is not (0x28 or 0x73) || instructions.Length - offset < 5)
        throw new InvalidOperationException("The baseline entry no longer has the expected straight-line IL.");
    var member = MetadataTokens.Handle(BinaryPrimitives.ReadInt32LittleEndian(instructions.AsSpan(offset + 1, 4)));
    if (opcode == 0x73 && member.Kind == HandleKind.MethodDefinition)
    {
        var ctor = metadata.GetMethodDefinition((MethodDefinitionHandle)member);
        var type = metadata.GetTypeDefinition(ctor.GetDeclaringType());
        if (type.BaseType.Kind != HandleKind.TypeReference)
            throw new InvalidOperationException("Expected an imported base type for the reference form.");
        var parent = metadata.GetTypeReference((TypeReferenceHandle)type.BaseType);
        if (parent.ResolutionScope.Kind != HandleKind.AssemblyReference)
            throw new InvalidOperationException("Expected a framework assembly reference.");
        var parentAssembly = metadata.GetAssemblyReference((AssemblyReferenceHandle)parent.ResolutionScope);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            entryType = FullName(declaring.Namespace, declaring.Name),
            entryMethod = metadata.GetString(entry.Name),
            ilOffset = offset,
            opcode = "newobj",
            targetType = FullName(type.Namespace, type.Name),
            targetMethod = metadata.GetString(ctor.Name),
            baseType = FullName(parent.Namespace, parent.Name),
            baseAssembly = metadata.GetString(parentAssembly.Name)
        }));
        return;
    }

    offset += 5;
}
throw new InvalidOperationException("No form constructor was found in the entry point.");

string FullName(StringHandle ns, StringHandle name) => metadata.GetString(ns) + "." + metadata.GetString(name);
