using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using BootstrapLibrary;
using ManagedFinalizationExperiment;

internal static class EmitRootProbe
{
    private delegate int CompileRoots(byte[] bundle, byte[] entries, byte[] output, int[] location);

    internal static int[] Write(string outputPath)
    {
        byte[] image = File.ReadAllBytes(typeof(RootProbe).Assembly.Location);
        byte[] bundle = new byte[image.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bundle, image.Length);
        image.CopyTo(bundle, sizeof(int));
        using var manifest = new MemoryStream();
        using var entries = new BinaryWriter(manifest);
        foreach (string name in new[] { nameof(RootProbe.ObserveFrames), nameof(RootProbe.ObserveConstructor),
            nameof(RootProbe.ThrowWithRoot), nameof(RootProbe.BuildGraph), nameof(RootProbe.SetStaticRoot) })
        {
            byte[] label = Encoding.UTF8.GetBytes(name);
            entries.Write(typeof(RootProbe).GetMethod(name)!.MetadataToken & 0x00ffffff);
            entries.Write(label.Length);
            entries.Write(label);
        }

        var compile = typeof(CompilerExports).Assembly.GetType("PortableCompiler.MethodCompiler", throwOnError: true)!
            .GetMethod("CompileBundleWithRootProbe", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<CompileRoots>();
        byte[] output = new byte[1 << 20];
        int[] location = new int[3];
        int length = compile(bundle, manifest.ToArray(), output, location);
        if (length <= 0) throw new Exception($"Root probe compiler returned {length} at {string.Join(", ", location)}");
        File.WriteAllBytes(outputPath, output[..length]);

        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            try { RootProbe.ThrowWithRoot(11); }
            catch (NullReferenceException) { return [RootProbe.ObserveFrames(11), RootProbe.ObserveConstructor(11)]; }
            throw new Exception("The CLR root probe did not throw NullReferenceException");
        }
        finally { Console.SetOut(original); }
    }

    internal static object DescribeGraph()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        TextWriter original = Console.Out;
        object root;
        try
        {
            Console.SetOut(TextWriter.Null);
            root = RootProbe.BuildGraph(11);
        }
        finally { Console.SetOut(original); }
        Type type = root.GetType();
        if (RootProbe.SetStaticRoot(root) is not null || !ReferenceEquals(RootProbe.SetStaticRoot(null), root))
            throw new Exception("Unexpected CLR static root storage");
        object next = type.GetField("Next", fields)!.GetValue(root)!;
        var items = (Array)type.GetField("Items", fields)!.GetValue(root)!;
        return new
        {
            fields = type.GetFields(fields).Count(field => !field.FieldType.IsValueType),
            arrayLength = items.Length,
            inherited = type.GetField("Inherited", fields)!.GetValue(root) is not null,
            cycle = ReferenceEquals(type.GetField("Next", fields)!.GetValue(next), root),
            arrayAliases = ReferenceEquals(items.GetValue(0), next) && ReferenceEquals(items.GetValue(2), root),
            nullEdges = (items.GetValue(1) is null ? 1 : 0) + (type.GetField("Missing", fields)!.GetValue(root) is null ? 1 : 0),
            reachable = Reachable(),
        };

        int Reachable()
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var pending = new Queue<object>();
            Add(root);
            while (pending.TryDequeue(out object item))
            {
                if (item is Array array)
                {
                    if (!item.GetType().GetElementType()!.IsValueType)
                        foreach (object value in array) Add(value);
                }
                else
                {
                    foreach (FieldInfo field in item.GetType().GetFields(fields))
                        if (!field.FieldType.IsValueType) Add(field.GetValue(item));
                }
            }
            return seen.Count;

            void Add(object item)
            {
                if (item is not null && seen.Add(item)) pending.Enqueue(item);
            }
        }
    }
}
