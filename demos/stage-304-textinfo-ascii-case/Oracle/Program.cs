using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace StandardWasm;

public static partial class Program
{
    private const int Count = 9;

    public static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
        using var core = new PEReader(File.OpenRead(args[1]));
        MetadataReader metadata = core.GetMetadataReader();
        var textInfo = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
            metadata.GetString(type.Namespace) == "System.Globalization" && metadata.GetString(type.Name) == "TextInfo");
        int lowerRow = MetadataTokens.GetRowNumber(textInfo.GetMethods().Single(handle =>
        {
            var method = metadata.GetMethodDefinition(handle);
            return metadata.GetString(method.Name) == "ToLowerAsciiInvariant" &&
                metadata.GetBlobBytes(method.Signature).SequenceEqual(new byte[] { 0, 1, 0x0e, 0x0e });
        }));
        File.WriteAllText(args[0], JsonSerializer.Serialize(new
        {
            runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
            assembly = typeof(AsciiCaseProbe).Assembly.Location,
            rows = new
            {
                lower = lowerRow,
                input = Row(nameof(AsciiCaseProbe.Input)),
                codeUnit = Row(nameof(AsciiCaseProbe.CodeUnit))
            },
            report = GetStockText()
        }));
    }

    private static int Row(string name) => typeof(AsciiCaseProbe).GetMethod(name)!.MetadataToken & 0xffffff;

#pragma warning disable CA1416 // The stock browser runtime invokes this export from its test harness.
    [System.Runtime.InteropServices.JavaScript.JSExport]
#pragma warning restore CA1416
    public static string GetStockText()
    {
        var lower = typeof(TextInfo).GetMethod("ToLowerAsciiInvariant", BindingFlags.NonPublic | BindingFlags.Static,
            binder: null, types: [typeof(string)], modifiers: null)!;
        var report = new StringBuilder();
        for (int index = 0; index < Count; index++)
        {
            string input = AsciiCaseProbe.Input(index);
            report.Append("ASCII|").Append(index).Append('|');
            try
            {
                var changed = (string)lower.Invoke(null, [input])!;
                report.Append(ReferenceEquals(input, changed) ? "same" : "fresh");
                foreach (char unit in changed) report.Append('|').Append((int)unit);
            }
            catch (TargetInvocationException error)
            {
                report.Append(error.InnerException!.GetType().Name);
            }
            report.Append('\n');
        }
        return report.ToString();
    }
}
