using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;

internal static class FrameworkEventCases
{
    private delegate int FindHeaps(byte[] image, int[] layout, int[] heaps);
    private delegate int ReadReference(byte[] image, int[] heaps, int[] layout, int coded);

    internal static int Verify(string corePath)
    {
        const BindingFlags internalStatic = BindingFlags.Static | BindingFlags.NonPublic;
        Assembly compiler = typeof(CompilerExports).Assembly;
        var find = compiler.GetType("PeLibrary.CompressedTables", throwOnError: true)!
            .GetMethod("FindWithHeaps", internalStatic)!.CreateDelegate<FindHeaps>();
        var reference = compiler.GetType("PeLibrary.EventMetadata", throwOnError: true)!
            .GetMethod("Reference", internalStatic)!.CreateDelegate<ReadReference>();
        Type layouts = compiler.GetType("PeLibrary.ObjectLayouts", throwOnError: true)!;
        byte[] image = File.ReadAllBytes(corePath);
        int[] layout = new int[4], heaps = new int[8];
        if (find(image, layout, heaps) < 0)
            throw new Exception("Could not read CoreLib metadata");

        using var pe = new PEReader(new MemoryStream(image, writable: false));
        MetadataReader metadata = pe.GetMetadataReader();
        string[] names = ["EventArgs", "EventHandler", "Delegate"];
        foreach (string name in names)
        {
            var handle = metadata.TypeDefinitions.Single(candidate =>
            {
                var definition = metadata.GetTypeDefinition(candidate);
                return metadata.StringComparer.Equals(definition.Namespace, "System") &&
                    metadata.StringComparer.Equals(definition.Name, name);
            });
            // The TypeDefOrRef tag for a definition is zero.
            int coded = MetadataTokens.GetRowNumber(handle) << 2;
            int expected = Convert.ToInt32(layouts.GetField(name + "Reference", internalStatic)!.GetRawConstantValue());
            if (reference(image, heaps, layout, coded) != expected)
                throw new Exception($"CoreLib System.{name} did not resolve to its framework representation");
        }
        return names.Length;
    }
}
