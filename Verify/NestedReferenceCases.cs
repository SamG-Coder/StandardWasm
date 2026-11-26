using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using BootstrapLibrary;
using NestedContract;
using NestedDemo;

internal static class NestedReferenceCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] caller = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] contract = File.ReadAllBytes(typeof(Holder).Assembly.Location);
        string forwarded = Path.Combine(AppContext.BaseDirectory, "nested-forwarding");
        byte[] facade = File.ReadAllBytes(Path.Combine(forwarded, "NestedContract.dll"));
        byte[] implementation = File.ReadAllBytes(Path.Combine(forwarded, "NestedImplementation.dll"));
        byte[] direct = BundleCases.Pack(caller, contract);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check("direct", direct, nameof(Kernels.Run), Kernels.Run);
        Check("friend", direct, nameof(Kernels.Friend), Kernels.Friend);
        using (var scope = new TestLoadScope(facade, implementation))
        {
            var run = scope.Entry(caller, nameof(Kernels.Run));
            Check("forwarded", BundleCases.Pack(caller, facade, implementation), nameof(Kernels.Run), run);
        }

        byte[] wrongKey = (byte[])contract.Clone();
        string grant = typeof(Holder).Assembly.GetCustomAttribute<InternalsVisibleToAttribute>()!.AssemblyName;
        int keyOffset = grant.IndexOf("PublicKey=", StringComparison.Ordinal) + "PublicKey=".Length;
        int grantOffset = wrongKey.AsSpan().IndexOf(Encoding.ASCII.GetBytes(grant));
        if (grantOffset < 0 || keyOffset >= grant.Length)
            throw new InvalidOperationException("Missing signed fixture grant.");
        int edited = grantOffset + keyOffset + 72;
        wrongKey[edited] = wrongKey[edited] == 'A' ? (byte)'B' : (byte)'A';
        using (var scope = new TestLoadScope(wrongKey, null))
        {
            var run = scope.Entry(caller, nameof(Kernels.Friend));
            try
            {
                run(5);
                throw new InvalidOperationException("CLR accepted the changed friend identity.");
            }
            catch (TypeAccessException) { }
            catch (MethodAccessException) { }
        }
        Reject("wrong friend key", BundleCases.Pack(caller, wrongKey), nameof(Kernels.Friend), OpCodes.Newobj, -2);

        int store = Row(nameof(Kernels.Store));
        using var pe = new PEReader(new MemoryStream(caller));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(store)).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int body = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (caller[body] & 3) == 2 ? 1 : (caller[body + 1] >> 4) * 4;
        byte[] changedCaller = (byte[])caller.Clone();
        int call = Offset(nameof(Kernels.Store), OpCodes.Call);
        int replacement = typeof(Kernels).GetMethod(nameof(Kernels.MakeRight))!.MetadataToken;
        BinaryPrimitives.WriteInt32LittleEndian(changedCaller.AsSpan(body + header + call + 1), replacement);
        Reject("wrong enclosing type", BundleCases.Pack(changedCaller, contract), nameof(Kernels.Store), OpCodes.Stfld, -1);

        void Check(string fixture, byte[] bundle, string method, Func<int, int> clr)
        {
            byte[] entries = Manifest(method);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{fixture} returned {size} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            var inputs = new[] { Encode(bundle), Encode(entries) };
            foreach (int value in new[] { int.MinValue, -3, 5, int.MaxValue })
            {
                int expected = clr(value);
                if (fixture == "forwarded" && expected != Kernels.Run(value))
                    throw new InvalidOperationException("The forwarded CLR deployment changed behavior.");
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "nested references " + fixture,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex }, expected
                });
            }
        }

        void Reject(string fixture, byte[] bundle, string method, OpCode opcode, int expected)
        {
            byte[] entries = Manifest(method);
            int[] diagnostic = [0, Row(method), Offset(method, opcode)];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nested references " + fixture,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected
            });
        }
    }

    private sealed class TestLoadScope(byte[] contract, byte[] implementation) : AssemblyLoadContext(isCollectible: true), IDisposable
    {
        internal Func<int, int> Entry(byte[] image, string method) =>
            LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!.GetMethod(method)!
                .CreateDelegate<Func<int, int>>();

        protected override Assembly Load(AssemblyName name) => name.Name switch
        {
            "NestedContract" => LoadFromStream(new MemoryStream(contract)),
            "NestedImplementation" when implementation != null => LoadFromStream(new MemoryStream(implementation)),
            _ => null
        };

        public void Dispose() => Unload();
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string name, OpCode opcode)
    {
        byte[] il = typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
        int offset = IlOffsets.Find(il, opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{name} did not emit {opcode}.");
    }

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
