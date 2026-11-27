using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

// Stage 129 (decision 0057). Run on the 32-bit CLR: its runtime directory supplies the
// supported CoreLib pack (decision 0044) bundled with the fixture, and its results are
// the expected values. Each kernel runs in a fresh collectible AssemblyLoadContext, so
// every case starts with no initializer run, as a fresh Wasm instance does.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]), dependency = Path.GetFullPath(args[2]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

var files = new List<string> { fixture, dependency };
foreach (string name in new[] { "netstandard", "System.Runtime", "System.Threading", "System.Private.CoreLib" })
    files.Add(Path.Combine(runtimeDirectory, name + ".dll"));
int[] images = Enumerable.Range(0, files.Count).ToArray();
var cases = new List<object>();

foreach (string name in new[]
{
    "ReachedOnce", "CallAfterArgument", "StoreAfterRightHand", "BeforeFieldInitOnce", "ConstructorTriggers",
    "InheritedField", "Recursion", "CatchInsideInitializer", "BundledDependency", "RelaxedGenericCall",
    "DistinctObjects", "StaticFieldAlias", "NotNominalClass", "Locked", "RuntimeTypeLock", "StaticAutoProperty"
})
{
    var (kernels, context) = Load(name);
    MethodInfo method = kernels.GetMethod(name)!;
    string value = ((int)method.Invoke(null, null)!).ToString();
    cases.Add(new { name, row = Row(method), images, clr = value, wasm = value });
    context.Unload();
}

// An exception escaping an initializer. The CLR makes one attempt and raises the same
// TypeInitializationException (with the same inner exception) on every later trigger.
// The compiler cannot construct that exception yet, so each trigger must trap instead,
// on the same instance, without a second attempt and without partial success.
Failure("ReadFailing", [("run", "ReadFailing"), ("trace", "ReadTrace"), ("run", "ReadFailing"), ("trace", "ReadTrace")]);
Failure("ReadOuterFailing", [("run", "ReadOuterFailing"), ("inner", "ReadFailing"), ("trace", "ReadTrace"),
    ("run", "ReadOuterFailing"), ("trace", "ReadTrace")]);

// A raw engine trap (Wasm stack exhaustion) inside an initializer. No CLR comparison:
// the CLR ends the process on stack overflow. The next host entry must retire the
// interrupted initializer, so a later trigger traps while an unrelated export still runs.
{
    var (kernels, context) = Load("ReadExhausting");
    MethodInfo run = kernels.GetMethod("ReadExhausting")!, trace = kernels.GetMethod("ReadTrace")!;
    cases.Add(new
    {
        name = "RawTrapRetired", row = Row(run), images, wasmOnly = true,
        exports = new[] { new { name = "run", row = Row(run) }, new { name = "trace", row = Row(trace) } },
        sequence = new[] { new[] { "run", "raw trap" }, new[] { "trace", "5" }, new[] { "run", "trap" }, new[] { "trace", "5" } }
    });
    context.Unload();
}

// Object members on a root object and a precise generic initializer stay located diagnostics.
foreach (string name in new[] { "RootHashCode", "RootToString", "RootGetType", "RootEquals", "PreciseGenericCall" })
{
    var (kernels, context) = Load(name);
    MethodInfo method = kernels.GetMethod(name)!;
    string clr = ((int)method.Invoke(null, null)!).ToString();
    byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
    int offset = IlOffset(il, name == "PreciseGenericCall" ? OpCodes.Call : OpCodes.Callvirt);
    cases.Add(new { name, row = Row(method), images, clr, status = -2, diagnostic = new[] { 0, Row(method), offset } });
    context.Unload();
}

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));

void Failure(string name, (string export, string method)[] calls)
{
    var (kernels, context) = Load(name);
    var thrown = new Dictionary<string, Exception>();
    var sequence = new List<string[]>();
    foreach (var (export, methodName) in calls)
    {
        MethodInfo method = kernels.GetMethod(methodName)!;
        try
        {
            sequence.Add([export, ((int)method.Invoke(null, null)!).ToString()]);
            continue;
        }
        catch (TargetInvocationException error) when (error.InnerException is TypeInitializationException initialization)
        {
            // Every later trigger of one type raises the identical exception object.
            if (thrown.TryGetValue(methodName, out Exception? first) && !ReferenceEquals(first, initialization))
                throw new InvalidOperationException(name + ": the CLR raised a different TypeInitializationException.");
            if (initialization.InnerException is not (DivideByZeroException or TypeInitializationException))
                throw new InvalidOperationException(name + ": unexpected inner exception " + initialization.InnerException);
            thrown[methodName] = initialization;
            sequence.Add([export, "TypeInitializationException"]);
        }
    }
    var exports = calls.Select(call => call.export).Distinct()
        .Select(export => new { name = export, row = Row(kernels.GetMethod(calls.First(call => call.export == export).method)!) })
        .ToArray();
    cases.Add(new { name, row = exports[0].row, images, exports, sequence });
    context.Unload();
}

(Type, AssemblyLoadContext) Load(string label)
{
    var context = new AssemblyLoadContext(label, isCollectible: true);
    context.Resolving += (_, assembly) => assembly.Name == "TypeInitializerDependency" ? context.LoadFromAssemblyPath(dependency) : null;
    return (context.LoadFromAssemblyPath(fixture).GetType("TypeInitializerFixture.Kernels", throwOnError: true)!, context);
}

static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

// Offset of the first instruction with this opcode, decoding every operand width.
static int IlOffset(byte[] il, OpCode target)
{
    var single = new Dictionary<int, OpCode>();
    var prefixed = new Dictionary<int, OpCode>();
    foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var code = (OpCode)field.GetValue(null)!;
        if (code.Size == 1) single[code.Value & 0xff] = code;
        else prefixed[code.Value & 0xff] = code;
    }
    for (int cursor = 0; cursor < il.Length;)
    {
        int offset = cursor;
        OpCode code = il[cursor] == 0xfe ? prefixed[il[cursor + 1]] : single[il[cursor]];
        if (code == target) return offset;
        cursor += code.Size;
        cursor += code.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, cursor),
            _ => 4
        };
    }
    throw new InvalidOperationException("No " + target + " instruction.");
}
