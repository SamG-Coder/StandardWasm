using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

// Run this oracle on the 32-bit CLR. Its own runtime directory is the
// supported pack, and its results are the 32-bit CLR contract.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], consumer = Path.GetFullPath(args[1]), wide = Path.GetFullPath(args[2]);
string narrow = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string? browser = args.Length > 3 ? Path.GetFullPath(args[3]) : null;

var files = new List<string> { consumer };
int narrowRuntime = Add(Path.Combine(narrow, "System.Runtime.dll"));
int narrowCore = Add(Path.Combine(narrow, "System.Private.CoreLib.dll"));
int wideRuntime = Add(Path.Combine(wide, "System.Runtime.dll"));
int wideCore = Add(Path.Combine(wide, "System.Private.CoreLib.dll"));

// A copy of the 32-bit CoreLib whose IntPtr.get_Size constant says 8. Its PE
// header still reads I386 and PE32, so the header alone can't reject it.
byte[] patched = File.ReadAllBytes(files[narrowCore]);
int sizeOperand = ConstantOperand(files[narrowCore], "IntPtr", "get_Size");
if (BinaryPrimitives.ReadInt32LittleEndian(patched.AsSpan(sizeOperand)) != 4)
    throw new InvalidOperationException("Expected the 32-bit IntPtr.get_Size constant.");
BinaryPrimitives.WriteInt32LittleEndian(patched.AsSpan(sizeOperand), 8);
string patchedPath = Path.Combine(evidence, "System.Private.CoreLib.width-8.dll");
File.WriteAllBytes(patchedPath, patched);
int patchedCore = Add(patchedPath);

const string patchedName = "win-x86 with IntPtr.Size patched to 8";
var packs = new List<object>
{
    Pack("win-x86", narrow, narrowCore, supported: true),
    Pack("win-x64", wide, wideCore, supported: false),
    Pack(patchedName, evidence, patchedCore, supported: false)
};
int[]? browserImages = null;
if (browser != null)
{
    string browserLib = Directory.GetDirectories(Path.Combine(browser, "runtimes", "browser-wasm", "lib")).Single();
    int browserRuntime = Add(Path.Combine(browserLib, "System.Runtime.dll"));
    int browserCore = Add(Path.Combine(browser, "runtimes", "browser-wasm", "native", "System.Private.CoreLib.dll"));
    packs.Add(Pack("browser-wasm", browser, browserCore, supported: true));
    browserImages = [0, browserRuntime, browserCore];
}

Type entry = Assembly.LoadFrom(consumer).GetType("NativeWidthConsumer.Entry", throwOnError: true)!;
var cases = new List<object>();
// Each consumer method and the CoreLib body it lowers first.
(string Method, string Type, string Callee)[] widthDependent =
[
    ("Size", "IntPtr", "get_Size"), ("UnsignedSize", "UIntPtr", "get_Size"),
    ("SignedMax", "IntPtr", "get_MaxValue"), ("SignedMin", "IntPtr", "get_MinValue"),
    ("UnsignedMax", "UIntPtr", "get_MaxValue"), ("WidenedSignedMax", "IntPtr", "get_MaxValue"),
    ("WidenedUnsignedMax", "UIntPtr", "get_MaxValue")
];
foreach (var (name, type, callee) in widthDependent)
{
    MethodInfo method = entry.GetMethod(name)!;
    object value = method.Invoke(null, null)!;
    Accept(method, "win-x86", [0, narrowRuntime, narrowCore], value);
    if (browserImages != null) Accept(method, "browser-wasm", browserImages, value);
    Reject(method, "win-x64", [0, wideRuntime, wideCore], CoreRow(files[wideCore], type, callee));
    Reject(method, patchedName, [0, narrowRuntime, patchedCore], CoreRow(files[patchedCore], type, callee));
}
MethodInfo allocate = entry.GetMethod("Allocate")!;
object allocated = allocate.Invoke(null, null)!;
Accept(allocate, "win-x86", [0, narrowRuntime, narrowCore], allocated);
// Metadata-only use of the 64-bit CoreLib remains available.
Accept(allocate, "win-x64", [0, wideRuntime, wideCore], allocated);

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, packs, runtime = Environment.Version.ToString(),
    process = RuntimeInformation.ProcessArchitecture.ToString()
}));

int Add(string path)
{
    files.Add(path);
    return files.Count - 1;
}

void Accept(MethodInfo method, string pack, int[] images, object value)
{
    // Decision 0027: a native integer result crosses to JavaScript as i32.
    string wasm = value switch
    {
        int v => v.ToString(),
        long v => v.ToString(),
        nint v => ((int)v).ToString(),
        nuint v => unchecked((int)(uint)v).ToString(),
        _ => throw new InvalidOperationException("Unexpected result type " + value.GetType())
    };
    cases.Add(new { name = method.Name, pack, row = method.MetadataToken & 0xffffff, images, clr = value.ToString(), wasm });
}

void Reject(MethodInfo method, string pack, int[] images, int coreRow) => cases.Add(new
{
    name = method.Name, pack, row = method.MetadataToken & 0xffffff, images,
    // CoreLib's position in the bundle, the callee's MethodDef row, no IL offset.
    status = -7, diagnostic = new[] { 2, coreRow, -1 }
});

object Pack(string name, string directory, int image, bool supported)
{
    byte[] bytes = File.ReadAllBytes(files[image]);
    using var pe = new PEReader(new MemoryStream(bytes));
    PEHeaders headers = pe.PEHeaders;
    return new
    {
        name, directory, file = files[image], image, supported,
        nativeWidth = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(ConstantOperand(files[image], "IntPtr", "get_Size"))),
        machine = headers.CoffHeader.Machine.ToString(), magic = headers.PEHeader!.Magic.ToString(),
        corFlags = headers.CorHeader!.Flags.ToString(),
        version = pe.GetMetadataReader().GetAssemblyDefinition().Version.ToString()
    };
}

static int CoreRow(string path, string type, string name)
{
    using var pe = new PEReader(File.OpenRead(path));
    return MetadataTokens.GetRowNumber(Find(pe.GetMetadataReader(), type, name));
}

// File offset of the operand in a tiny-header `ldc.i4 N; ret` body.
static int ConstantOperand(string path, string type, string name)
{
    using var pe = new PEReader(File.OpenRead(path));
    MetadataReader metadata = pe.GetMetadataReader();
    MethodDefinition method = metadata.GetMethodDefinition(Find(metadata, type, name));
    byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
    if (il.Length != 6 || il[0] != 0x20 || il[5] != 0x2a)
        throw new InvalidOperationException($"Unexpected {type}.{name} body in {path}.");
    int rva = method.RelativeVirtualAddress;
    SectionHeader section = pe.PEHeaders.SectionHeaders.Single(s =>
        rva >= s.VirtualAddress && rva < s.VirtualAddress + s.VirtualSize);
    // One tiny-header byte and the ldc.i4 opcode precede the operand.
    return rva - section.VirtualAddress + section.PointerToRawData + 2;
}

static MethodDefinitionHandle Find(MetadataReader metadata, string type, string name)
{
    foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
    {
        TypeDefinition definition = metadata.GetTypeDefinition(handle);
        if (metadata.GetString(definition.Namespace) != "System" || metadata.GetString(definition.Name) != type) continue;
        foreach (MethodDefinitionHandle method in definition.GetMethods())
            if (metadata.GetString(metadata.GetMethodDefinition(method).Name) == name) return method;
    }
    throw new InvalidOperationException($"Missing System.{type}.{name}.");
}
