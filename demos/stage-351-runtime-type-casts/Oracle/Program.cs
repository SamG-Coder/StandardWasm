using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using BootstrapLibrary;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var runtimeType = typeof(Type).Assembly.GetType("System.RuntimeType", throwOnError: true);
var test = CreateProbe(OpCodes.Isinst);
var cast = CreateProbe(OpCodes.Castclass);
var cases = new[]
{
    Observe("runtime type", typeof(int)),
    Observe("different runtime type", typeof(string)),
    Observe("module", typeof(int).Module),
    Observe("assembly", typeof(int).Assembly),
    Observe("other object", new TypeDelegator(typeof(int))),
    Observe("null", null)
};

// Run the product emitter against exact global identities. The test module
// supplies an impostor with the same Wasm shape as the canonical descriptors.
Assembly compiler = typeof(CompilerExports).Assembly;
Type emitter = compiler.GetType("PortableCompiler.RuntimeTypeWasm", throwOnError: true);
Type planType = compiler.GetType("PortableCompiler.RuntimeReflectionPlan", throwOnError: true);
Type fields = compiler.GetType("PortableCompiler.StaticFields", throwOnError: true);
Type fixups = compiler.GetType("PortableCompiler.WasmIndexFixups", throwOnError: true);
const BindingFlags members = BindingFlags.Static | BindingFlags.NonPublic;
var globals = new int[64];
var rows = new[] { 42, 61, -1, -2, 42 };
for (int index = 0; index < rows.Length; index++)
{
    int owner = index < rows.Length - 1 ? -3 : 0;
    fields.GetMethod("Register", members).Invoke(null, [globals, owner, rows[index], 1, 0]);
}

var helper = new byte[256];
int used = (int)emitter.GetMethod("WriteCastHelper", members).Invoke(null, [helper, 0, globals]);
if (used <= 0) throw new InvalidOperationException("Could not emit the runtime type cast helper.");

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    cases,
    rows,
    helper = Convert.ToBase64String(helper, 0, used),
    test = WriteCast(false, false),
    cast = WriteCast(false, true),
    proven = WriteCast(true, true)
}));

Func<object, object> CreateProbe(OpCode operation)
{
    var method = new DynamicMethod("check", typeof(object), new[] { typeof(object) },
        typeof(Program).Module, skipVisibility: true);
    ILGenerator il = method.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(operation, runtimeType);
    il.Emit(OpCodes.Ret);
    return method.CreateDelegate<Func<object, object>>();
}

object Observe(string name, object source)
{
    object result = test(source);
    bool preserved = result != null && ReferenceEquals(result, source);
    try
    {
        return new { name, preserved, cast = ReferenceEquals(cast(source), source) ? "identity" : "wrong value" };
    }
    catch (InvalidCastException)
    {
        return new { name, preserved, cast = "invalid cast" };
    }
}

string WriteCast(bool proven, bool throwing)
{
    var body = new byte[256];
    // Exercise both distinct spills and a cast that overwrites its source.
    int target = throwing && !proven ? 0 : 1;
    // One eqref local at index 1 is available. Function 0 is the helper.
    body[0] = 1;
    body[1] = 1;
    body[2] = 0x6d;
    object plan = Activator.CreateInstance(planType, nonPublic: true);
    int size = (int)emitter.GetMethod("Cast", members).Invoke(null, [body, 3, 0, 0, target, proven, throwing, plan]);
    var chain = (int[])planType.GetField("RuntimeTypeCastFixups", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plan);
    fixups.GetMethod("Resolve", members).Invoke(null, [body, chain[0], 0]);
    body[size++] = 0x20;
    body[size++] = (byte)target;
    body[size++] = 0x0b;
    return Convert.ToBase64String(body, 0, size);
}
