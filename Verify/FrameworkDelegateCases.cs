using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using FrameworkDelegateDemo;
using FrameworkDelegateDependency;

// Stage 119: EventHandler delegates reached through System.Runtime, across linked event accessors.
internal static class FrameworkDelegateCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] publisher = File.ReadAllBytes(typeof(Publisher).Assembly.Location);
        byte[] bundle = BundleCases.Pack(entry, publisher);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        Check(nameof(Kernels.Run), Kernels.Run);
        Check(nameof(Kernels.Subscribe), Kernels.Subscribe);
        Check(nameof(Kernels.Closure), Kernels.Closure);
        Check(nameof(Kernels.Combine), Kernels.Combine);
        Check(nameof(Kernels.Unsubscribe), Kernels.Unsubscribe);
        Check(nameof(Kernels.Identity), Kernels.Identity);

        // The facade assembly exists but cannot resolve this TypeRef on the CLR.
        if (Assembly.Load("System.Runtime.Extensions").GetType("System.EventHandler") != null ||
            Assembly.Load("System.Runtime").GetType("System.Threading.Interlocked") != null)
            throw new InvalidOperationException("The installed CLR unexpectedly forwards a negative fixture's type.");
        RejectFacade("event through Runtime.Extensions", "System.Runtime", "System.Runtime.Extensions");
        RejectFacade("Interlocked through Runtime", "System.Threading", "System.Runtime");

        // Stage 164 (decision 0088): an open static EventHandler target now binds a null target
        // through a thunk that ignores it, so this former rejection is compared with the CLR.
        // (The kernel keeps its Stage 119 name.)
        Check(nameof(Kernels.RejectStatic), Kernels.RejectStatic);

        void Check(string name, Func<int, int> expectedResult)
        {
            MethodInfo run = Method(name);
            byte[] entries = Manifest(run);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"Framework delegates {name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int input in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "framework delegates " + name,
                    inputs = new[] { Encode(bundle), Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = expectedResult(input)
                });
        }

        void RejectFacade(string name, string from, string to)
        {
            byte[] invalidBundle = BundleCases.Pack(entry, RedirectAssemblyName(publisher, from, to));
            byte[] entries = Manifest(Method(nameof(Kernels.Subscribe)));
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(invalidBundle, entries, output, location);
            if (status >= 0 || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} unexpectedly compiled with {status}.");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "framework delegates " + name,
                inputs = new[] { Encode(invalidBundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = (int[])location.Clone() },
                expected = status
            });
        }

        void Reject(MethodBase method, int[] diagnostic)
        {
            byte[] entries = Manifest(method);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method.Name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "framework delegates " + method.Name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] RedirectAssemblyName(byte[] image, string from, string to)
    {
        byte[] patched = (byte[])image.Clone();
        using var pe = new PEReader(new MemoryStream(image));
        var md = pe.GetMetadataReader();
        StringHandle name = to == "System.Runtime.Extensions"
            ? md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == "FacadeName").Namespace
            : md.AssemblyReferences.Select(md.GetAssemblyReference).Single(a => md.GetString(a.Name) == to).Name;
        var source = md.AssemblyReferences.Single(h => md.GetString(md.GetAssemblyReference(h).Name) == from);
        int strings = md.GetHeapSize(HeapIndex.String) < 65536 ? 2 : 4;
        int blobs = md.GetHeapSize(HeapIndex.Blob) < 65536 ? 2 : 4;
        int rowSize = md.GetTableRowSize(TableIndex.AssemblyRef);
        if (rowSize != 12 + 2 * strings + 2 * blobs)
            throw new InvalidOperationException("Unexpected AssemblyRef row layout.");
        int column = pe.PEHeaders.MetadataStartOffset + md.GetTableMetadataOffset(TableIndex.AssemblyRef) +
            (MetadataTokens.GetRowNumber(source) - 1) * rowSize + 12 + blobs;
        int offset = MetadataTokens.GetHeapOffset(name);
        if (strings == 2) BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(column), checked((ushort)offset));
        else BinaryPrimitives.WriteInt32LittleEndian(patched.AsSpan(column), offset);
        return patched;
    }

    private static MethodInfo Method(string name) => typeof(Kernels).GetMethod(name)!;
    private static int Row(MethodBase method) => method.MetadataToken & 0xffffff;

    private static int Offset(MethodBase method, OpCode opcode, int skip = 0)
    {
        int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode, skip);
        if (offset < 0) throw new InvalidOperationException($"{method.Name} lacks {opcode}.");
        return offset;
    }

    private static byte[] Manifest(MethodBase method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
