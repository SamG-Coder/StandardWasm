using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace StandardWasm;

public static partial class Program
{
    private const int OrdinarySource = 0, NullSource = 1, InvalidSource = 2;
    private static readonly Case[] Cases =
    [
        new("range", OrdinarySource, 6, 2, 3),
        new("exact", OrdinarySource, 3, 0, 3),
        new("lastByte", OrdinarySource, 4, 3, 1),
        new("emptyAtEnd", InvalidSource, 3, 3, 0),
        new("emptyArray", InvalidSource, 0, 0, 0),
        new("nullSourceWithZeroLength", NullSource, 3, 0, 0),
        new("nullDestination", OrdinarySource, -1, 0, 3),
        new("bothNull", NullSource, -1, 0, 3),
        new("negativeStart", OrdinarySource, 3, -1, 0),
        new("negativeLength", OrdinarySource, 3, 0, -1),
        new("beyondStart", OrdinarySource, 3, 4, 0),
        new("beyondLength", OrdinarySource, 3, 1, 3),
        new("tooLarge", OrdinarySource, 3, 1, int.MaxValue)
    ];

    private static readonly byte[] Input = [13, 65, 254];

    public static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
        TransferEmitterProbe.Write(args[1]);
        File.WriteAllText(args[0], JsonSerializer.Serialize(new
        {
            runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
            assembly = typeof(NativeByteTransferProbe).Assembly.Location,
            rows = new
            {
                transfer = Row(nameof(NativeByteTransferProbe.Transfer)),
                make = Row(nameof(NativeByteTransferProbe.Make)),
                read = Row(nameof(NativeByteTransferProbe.Read))
            },
            wrapperFrontier = JoinCoreRow(args[2]),
            input = Input.Select(value => (int)value).ToArray(),
            cases = Cases,
            report = GetStockText()
        }));
    }

    private static int JoinCoreRow(string coreLibrary)
    {
        using var core = new PEReader(File.OpenRead(coreLibrary));
        MetadataReader metadata = core.GetMetadataReader();
        var type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(definition =>
            metadata.GetString(definition.Namespace) == "System" && metadata.GetString(definition.Name) == "String");
        return MetadataTokens.GetRowNumber(type.GetMethods().Single(handle =>
        {
            var method = metadata.GetMethodDefinition(handle);
            byte[] signature = metadata.GetBlobBytes(method.Signature);
            return metadata.GetString(method.Name) == "JoinCore" && signature.Length > 5 &&
                signature[1] == 2 && signature[^1] == 0x1c;
        }));
    }
    private static int Row(string name) => typeof(NativeByteTransferProbe).GetMethod(name)!.MetadataToken & 0xffffff;

#pragma warning disable CA1416 // The stock browser runtime invokes this export from its test harness.
    [System.Runtime.InteropServices.JavaScript.JSExport]
#pragma warning restore CA1416
    public static unsafe string GetStockText()
    {
        var result = new StringBuilder();
        foreach (var test in Cases)
        {
            byte[] array = test.Destination < 0 ? null! : NativeByteTransferProbe.Make(test.Destination);
            byte[] alias = array;
            fixed (byte* address = Input)
            {
                nint source = test.Source == NullSource ? 0 : test.Source == InvalidSource ? -1 : (nint)address;
                result.Append("TRANSFER|").Append(test.Name);
                try
                {
                    NativeByteTransferProbe.Transfer(source, array, test.Start, test.Count);
                    result.Append("|ok");
                }
                catch (ArgumentException error)
                {
                    result.Append('|').Append(error.GetType().Name);
                }
            }
            if (alias != null)
                for (int index = 0; index < alias.Length; index++)
                    result.Append('|').Append(NativeByteTransferProbe.Read(alias, index));
            result.Append('\n');
        }
        return result.ToString();
    }

    private sealed record Case(string Name, int Source, int Destination, int Start, int Count);
}
