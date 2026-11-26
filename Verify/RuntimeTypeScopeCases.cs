#nullable enable

using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;

internal static class RuntimeTypeScopeCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        const string runtime = "System.Runtime", extensions = "System.Runtime.Extensions", reflection = "System.Reflection";
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, 1);
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));

        Check("runtime control", Image(runtime, runtime), 1, -1);
        Check("non-forwarding method owner", Image(extensions, runtime), -4, 5, clrRejects: true);
        Check("non-forwarding handle parameter", Image(runtime, extensions), -4, 5, clrRejects: true);
        Check("non-forwarding result type", Image(runtime, runtime, returnScope: extensions), -4, 5, clrRejects: true);
        Check("MemberInfo facade", Image(runtime, runtime, reflection, "MemberInfo"), -2, 10);
        Check("TypeInfo facade", Image(runtime, runtime, reflection, "TypeInfo"), -2, 10);

        void Check(string name, byte[] image, int expected, int offset, bool clrRejects = false)
        {
            try
            {
                var method = Assembly.Load(image).GetType("ScopeProbe")!.GetMethod("Run")!;
                if (!Equals(method.Invoke(null, null), 1) || clrRejects)
                    throw new InvalidOperationException(name + " unexpectedly ran on the CLR.");
            }
            catch (TargetInvocationException error) when (clrRejects &&
                (error.InnerException is TypeLoadException || error.InnerException is MissingMethodException))
            {
            }

            byte[] bundle = BundleCases.Pack(image);
            byte[] output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var location = new int[3];
            int actual = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            var inputs = new[] { Encode(bundle), Encode(entries) };
            string fixture = "runtime type scope " + name;
            if (expected > 0)
            {
                if (actual <= 0) throw new InvalidOperationException($"{fixture} returned {actual}.");
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture, inputs, expected,
                    generated = new { capacity, bundle = true, arguments = Array.Empty<int>(),
                        hex = Convert.ToHexString(output.AsSpan(0, actual)).ToLowerInvariant() }
                });
                return;
            }

            int[] diagnostic = [0, 1, offset];
            if (actual != expected || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {actual} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture, inputs, expected,
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic }
            });
        }
    }

    // Construct precise TypeRef scopes that the C# compiler normally selects for us.
    private static byte[] Image(string ownerScope, string handleScope, string? castScope = null, string? castName = null, string returnScope = "System.Runtime")
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("ScopeProbe.dll"), metadata.GetOrAddGuid(Guid.Empty), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("ScopeProbe"), new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var references = new Dictionary<string, AssemblyReferenceHandle>();
        var objectType = Reference("System.Runtime", "System", "Object");
        var owner = Reference(ownerScope, "System", "Type");
        var type = Reference(returnScope, "System", "Type");
        var handle = Reference(handleScope, "System", "RuntimeTypeHandle");
        var entry = MetadataTokens.MethodDefinitionHandle(1);
        var field = MetadataTokens.FieldDefinitionHandle(1);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default, field, entry);
        var probe = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("ScopeProbe"), objectType, field, entry);
        var signature = new BlobBuilder();
        signature.WriteBytes(new byte[] { 0, 1, 0x12 });
        signature.WriteCompressedInteger((MetadataTokens.GetRowNumber(type) << 2) | 1);
        signature.WriteByte(0x11);
        signature.WriteCompressedInteger((MetadataTokens.GetRowNumber(handle) << 2) | 1);
        var getType = metadata.AddMemberReference(owner, metadata.GetOrAddString("GetTypeFromHandle"), metadata.GetOrAddBlob(signature));
        var bodies = new BlobBuilder();
        var il = new InstructionEncoder(new BlobBuilder());
        il.OpCode(ILOpCode.Ldtoken);
        il.Token(probe);
        il.Call(getType);
        if (castScope != null)
        {
            il.OpCode(ILOpCode.Isinst);
            il.Token(Reference(castScope, "System.Reflection", castName!));
            il.OpCode(ILOpCode.Ldnull);
            il.OpCode(ILOpCode.Cgt_un);
        }
        else
        {
            il.OpCode(ILOpCode.Pop);
            il.LoadConstantI4(1);
        }
        il.OpCode(ILOpCode.Ret);
        int body = new MethodBodyStreamEncoder(bodies).AddMethodBody(il, maxStack: 2);
        metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
            metadata.GetOrAddString("Run"), metadata.GetOrAddBlob(new byte[] { 0, 0, 8 }), body, MetadataTokens.ParameterHandle(1));
        var result = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata), bodies).Serialize(result);
        return result.ToArray();

        TypeReferenceHandle Reference(string assembly, string space, string name)
        {
            if (!references.TryGetValue(assembly, out var reference))
            {
                AssemblyName identity = Assembly.Load(assembly).GetName();
                reference = metadata.AddAssemblyReference(metadata.GetOrAddString(assembly), identity.Version!, default,
                    metadata.GetOrAddBlob(identity.GetPublicKeyToken()!), 0, default);
                references.Add(assembly, reference);
            }
            return metadata.AddTypeReference(reference, metadata.GetOrAddString(space), metadata.GetOrAddString(name));
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
