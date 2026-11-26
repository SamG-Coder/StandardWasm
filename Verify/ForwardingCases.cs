#nullable enable

using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using ForwardingDemo;

internal static class ForwardingCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        string installed = Path.Combine(AppContext.BaseDirectory, "forwarding");
        string facade = Path.Combine(installed, "ForwardContract.dll");
        string implementation = Path.Combine(installed, "ForwardImplementation.dll");
        byte[] main = File.ReadAllBytes(typeof(Entry).Assembly.Location);
        byte[] redirect = File.ReadAllBytes(facade);
        byte[] target = File.ReadAllBytes(implementation);
        var method = typeof(Entry).GetMethod(nameof(Entry.Run))!;
        int row = method.MetadataToken & 0xffffff;
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        var context = new ForwardingContext(facade, implementation);
        try
        {
            var caller = context.LoadFromStream(new MemoryStream(main));
            var run = caller.GetType(typeof(Entry).FullName!)!.GetMethod(nameof(Entry.Run))!
                .CreateDelegate<Func<int, int>>();
            int[] values = [int.MinValue, -3, 5, int.MaxValue];
            int[] expected = values.Select(run).ToArray();
            if (!expected.SequenceEqual(values.Select(Entry.Run)) ||
                context.Assemblies.Single(a => a.GetName().Name == "ForwardContract").GetForwardedTypes()
                    .Single(t => t.FullName == "Forwarded.Box").Assembly.GetName().Name != "ForwardImplementation")
                throw new InvalidOperationException("Expected the CLR to keep behavior with the replacement facade.");

            var output = new byte[capacity];
            string hex = Check("forwarded members and class identity", BundleCases.Pack(main, redirect, target));
            if (Check("reordered facade", BundleCases.Pack(main, target, redirect)) != hex)
                throw new InvalidOperationException("Changing pack order changed the output.");
            Reject("missing forwarded implementation", BundleCases.Pack(main, redirect), -4);
            Reject("duplicate forwarded implementation", BundleCases.Pack(main, redirect, target, target), -5);

            var contractName = AssemblyName.GetAssemblyName(facade);
            var implementationName = AssemblyName.GetAssemblyName(implementation);
            Reject("cyclic type redirection", BundleCases.Pack(main, redirect,
                CycleLink(implementationName, contractName)), -1);

            string Check(string name, byte[] bundle)
            {
                var location = CompilerExports.CreateDiagnostic();
                int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
                if (size <= 0) throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}]");
                string wasm = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
                object[] inputs = [Encode(bundle), Encode(manifest)];
                for (int i = 0; i < values.Length; i++)
                    cases.Add(new
                    {
                        module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                        inputs, generated = new { capacity, bundle = true, arguments = new[] { values[i] }, hex = wasm },
                        expected = expected[i]
                    });
                return wasm;
            }

            void Reject(string name, byte[] bundle, int expectedStatus)
            {
                Array.Fill(output, (byte)0xa5);
                int status = CompilerExports.CompileBundle(bundle, manifest, output);
                if (status != expectedStatus || output.Any(value => value != 0xa5))
                    throw new InvalidOperationException($"{name} returned {status}");
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                    inputs = new[] { Encode(bundle), Encode(manifest) },
                    generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true },
                    expected = status
                });
            }
        }
        finally { context.Unload(); }
    }

    private sealed class ForwardingContext(string facade, string implementation) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) => name.Name switch
        {
            "ForwardContract" => LoadFromAssemblyPath(facade),
            "ForwardImplementation" => LoadFromAssemblyPath(implementation),
            _ => null
        };
    }

    private static byte[] CycleLink(AssemblyName from, AssemblyName to)
    {
        // A source compiler resolves the destination before emitting a facade.
        // Write the final cyclic edge directly as metadata for the diagnostic case.
        var md = new MetadataBuilder();
        md.AddModule(0, md.GetOrAddString(from.Name! + ".dll"), md.GetOrAddGuid(Guid.Empty), default, default);
        md.AddAssembly(md.GetOrAddString(from.Name!), from.Version!, default,
            md.GetOrAddBlob(from.GetPublicKey()!), AssemblyFlags.PublicKey, AssemblyHashAlgorithm.Sha1);
        var reference = md.AddAssemblyReference(md.GetOrAddString(to.Name!), to.Version!, default,
            md.GetOrAddBlob(to.GetPublicKeyToken()!), 0, default);
        md.AddTypeDefinition(TypeAttributes.NotPublic, default, md.GetOrAddString("<Module>"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        md.AddExportedType((TypeAttributes)0x00200000, md.GetOrAddString("Forwarded"),
            md.GetOrAddString("Box"), reference, 0);
        var result = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(md), new BlobBuilder()).Serialize(result);
        return result.ToArray();
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
