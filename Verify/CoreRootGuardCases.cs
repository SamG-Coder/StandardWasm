#nullable enable

using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;

internal static class CoreRootGuardCases
{
    private const int ChildConstructorRow = 2, EntryRow = 3, SizeRow = 4, IncompatibleNativeWidth = -7;

    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        const string coreLibName = "System.Private.CoreLib";
        byte[]? publicKey = typeof(object).Assembly.GetName().GetPublicKey();
        if (publicKey == null || publicKey.Length == 0)
            throw new InvalidOperationException("The CoreLib fixture requires the runtime's public key metadata.");

        var manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, EntryRow);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));

        Reject("CoreLib root with a different assembly name", Image("ThirdParty", publicKey), [0, EntryRow, 0]);
        Reject("CoreLib root without a public key", Image(coreLibName, []), [0, EntryRow, 0]);
        Reject("CoreLib Object constructor with a nonempty body",
            Image(coreLibName, publicKey, nonemptyConstructor: true), [0, ChildConstructorRow, 1]);
        // Decision 0044: CoreLib bodies are lowered only from a CoreLib whose
        // own IntPtr.get_Size returns the wasm32 native width.
        Reject("CoreLib without IntPtr.Size evidence", Image(coreLibName, publicKey, nativeWidth: null),
            [0, EntryRow, -1], IncompatibleNativeWidth);
        Reject("CoreLib built for an 8-byte native width", Image(coreLibName, publicKey, nativeWidth: 8),
            [0, EntryRow, -1], IncompatibleNativeWidth);

        byte[] control = BundleCases.Pack(Image(coreLibName, publicKey));
        byte[] output = new byte[capacity];
        int length = CompilerExports.CompileBundle(control, manifest, output);
        if (length <= 0) throw new InvalidOperationException($"Empty CoreLib constructor returned {length}.");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "CoreLib empty Object constructor",
            inputs = new[] { Encode(control), Encode(manifest) }, generated = new { capacity, bundle = true, arguments = new[] { 5 },
                hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant() }, expected = 5
        });

        void Reject(string fixture, byte[] image, int[] diagnostic, int expectedStatus = -2)
        {
            byte[] bundle = BundleCases.Pack(image);
            byte[] rejected = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var actual = new int[3];
            int status = CompilerExports.CompileBundleDetailed(bundle, manifest, rejected, actual);
            if (status != expectedStatus || !actual.SequenceEqual(diagnostic) || rejected.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {status} at [{string.Join(",", actual)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { Encode(bundle), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    internal static byte[] RuntimeTypeCoreLibImage() =>
        Image("System.Private.CoreLib", typeof(object).Assembly.GetName().GetPublicKey()!, includeType: true);

    private static byte[] Image(string name, byte[] key, bool nonemptyConstructor = false, int? nativeWidth = 4,
        bool includeType = false)
    {
        // Emit only the metadata seen by the compiler. The regular Stage 81
        // oracle separately exercises the installed CoreLib on the CLR.
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"), metadata.GetOrAddGuid(Guid.Empty), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 0), default, metadata.GetOrAddBlob(key),
            key.Length == 0 ? 0 : AssemblyFlags.PublicKey, AssemblyHashAlgorithm.Sha1);

        var fields = MetadataTokens.FieldDefinitionHandle(1);
        var parameters = MetadataTokens.ParameterHandle(1);
        var rootConstructor = MetadataTokens.MethodDefinitionHandle(1);
        var childConstructor = MetadataTokens.MethodDefinitionHandle(ChildConstructorRow);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"),
            default, fields, rootConstructor);
        var root = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("System"),
            metadata.GetOrAddString("Object"), default, fields, rootConstructor);
        metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Child"),
            root, fields, childConstructor);
        metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Entry"),
            root, fields, MetadataTokens.MethodDefinitionHandle(EntryRow));
        if (nativeWidth != null)
            metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Sealed, metadata.GetOrAddString("System"),
                metadata.GetOrAddString("IntPtr"), root, fields, MetadataTokens.MethodDefinitionHandle(SizeRow));

        if (includeType)
            metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Abstract, metadata.GetOrAddString("System"),
                metadata.GetOrAddString("Type"), root, fields, MetadataTokens.MethodDefinitionHandle(SizeRow + 1));

        var bodies = new BlobBuilder();
        var bodyStream = new MethodBodyStreamEncoder(bodies);
        int rootBody = Body(il =>
        {
            if (nonemptyConstructor)
            {
                il.OpCode(ILOpCode.Ldnull);
                il.OpCode(ILOpCode.Throw);
            }
            else il.OpCode(ILOpCode.Ret);
        });
        int childBody = Body(il =>
        {
            il.LoadArgument(0);
            il.Call(rootConstructor);
            il.OpCode(ILOpCode.Ret);
        });
        int entryBody = Body(il =>
        {
            il.OpCode(ILOpCode.Newobj);
            il.Token(childConstructor);
            il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0);
            il.OpCode(ILOpCode.Ret);
        });

        var ctorName = metadata.GetOrAddString(".ctor");
        var ctorSignature = metadata.GetOrAddBlob(new byte[] { 0x20, 0, 1 }); // instance void ()
        const MethodAttributes ctor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        metadata.AddMethodDefinition(ctor, MethodImplAttributes.IL, ctorName, ctorSignature, rootBody, parameters);
        metadata.AddMethodDefinition(ctor, MethodImplAttributes.IL, ctorName, ctorSignature, childBody, parameters);
        metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
            metadata.GetOrAddString("Run"), metadata.GetOrAddBlob(new byte[] { 0, 1, 8, 8 }), entryBody, parameters);
        if (nativeWidth is int width)
        {
            // The shape of the original CoreLib body: ldc.i4 N; ret.
            int sizeBody = Body(il =>
            {
                il.OpCode(ILOpCode.Ldc_i4);
                il.CodeBuilder.WriteInt32(width);
                il.OpCode(ILOpCode.Ret);
            });
            metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName,
                MethodImplAttributes.IL, metadata.GetOrAddString("get_Size"), metadata.GetOrAddBlob(new byte[] { 0, 0, 8 }),
                sizeBody, parameters);
        }

        var result = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata), bodies).Serialize(result);
        return result.ToArray();

        int Body(Action<InstructionEncoder> write)
        {
            var il = new InstructionEncoder(new BlobBuilder());
            write(il);
            return bodyStream.AddMethodBody(il, maxStack: 1);
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
