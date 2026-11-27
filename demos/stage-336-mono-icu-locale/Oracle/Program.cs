using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string mono = args[1];
using var pe = new PEReader(File.OpenRead(mono));
MetadataReader image = pe.GetMetadataReader();
int wrapper = MetadataTokens.GetRowNumber(image.MethodDefinitions.Single(handle =>
{
    MethodDefinition method = image.GetMethodDefinition(handle);
    return image.GetString(method.Name) == "IsPredefinedLocale" &&
        image.GetBlobBytes(method.Signature).SequenceEqual(new byte[] { 0, 1, 2, 0x0e });
}));

// Common stock CLR validity cases shared by Windows and the browser ICU profile.
int[] common = [0, 1, 2, 3, 5, 8, 10, 12];
var clr = common.Select(index => new { index, result = Predefined(LocaleInputs.Input(index)) });
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(LocaleInputs).Assembly.Location,
    row = typeof(LocaleInputs).GetMethod(nameof(LocaleInputs.Input))!.MetadataToken & 0xffffff,
    wrapper,
    clr
}));

static int Predefined(string name)
{
    try { _ = CultureInfo.GetCultureInfo(name, predefinedOnly: true); return 1; }
    catch (CultureNotFoundException) { return 0; }
}
