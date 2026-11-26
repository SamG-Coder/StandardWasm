using System.Reflection;
using System.Reflection.Metadata;

namespace WinFormsCensus;

/// <summary>
/// Static feature tags for one reached method. These are the fallback when
/// the compiler's verdict is unavailable, and they size each feature group
/// independently of the order in which the compiler meets blockers.
/// </summary>
internal static class Features
{
    // Namespace prefixes whose calls indicate threading or synchronization.
    private static readonly string[] ThreadingTypes =
    [
        "System.Threading.Monitor", "System.Threading.Interlocked", "System.Threading.Volatile", "System.Threading.Thread",
        "System.Threading.Lock", "System.Threading.ThreadPool", "System.Threading.SynchronizationContext",
        "System.Threading.ExecutionContext", "System.Threading.Tasks.", "System.Threading.WaitHandle",
        "System.Threading.ManualResetEvent", "System.Threading.AutoResetEvent", "System.Threading.EventWaitHandle",
        "System.Threading.SpinLock", "System.Threading.SpinWait", "System.Threading.Timer", "System.Threading.ReaderWriterLock",
        "System.Threading.SemaphoreSlim", "System.Threading.Mutex", "System.Threading.ThreadLocal",
        "System.Threading.AsyncLocal", "System.Threading.CancellationToken", "System.Threading.LazyInitializer",
    ];

    public static SortedSet<string> Classify(AssemblySet set, MethodId method, List<Instruction>? body, out (string Dll, string Entry)? pinvoke)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        pinvoke = null;
        LoadedAssembly assembly = set.All[method.Assembly];
        MetadataReader md = assembly.Metadata;
        MethodDefinition definition = set.Definition(method);
        TypeFacts owner = set.TypeOf(method);
        string name = md.GetString(definition.Name);

        if (definition.GetGenericParameters().Count > 0) tags.Add("generic method definition");
        if (owner.IsGenericDefinition) tags.Add("member of generic type");
        if (name == ".cctor") tags.Add(owner.Id.Row == 1 ? "module initializer" : "static constructor");
        if (owner.IsExplicitLayout) tags.Add("explicit layout (declaring type)");
        if (owner.IsValueType && (definition.Attributes & MethodAttributes.Static) == 0) tags.Add("value-type instance method");
        if (owner.IsComImport) tags.Add("COM import type");
        if ((definition.ImplAttributes & MethodImplAttributes.Synchronized) != 0) tags.Add("threading: synchronized method");
        if ((definition.Attributes & MethodAttributes.PinvokeImpl) != 0)
        {
            MethodImport import = definition.GetImport();
            string dll = md.GetString(md.GetModuleReference(import.Module).Name);
            string entry = import.Name.IsNil ? name : md.GetString(import.Name);
            pinvoke = (NormalizeDll(dll), entry);
            tags.Add("P/Invoke");
        }
        if ((definition.ImplAttributes & MethodImplAttributes.InternalCall) != 0) tags.Add("runtime internal call");
        if ((definition.ImplAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.Runtime)
            tags.Add(owner.IsDelegate ? "runtime delegate method" : "runtime-implemented method");
        if (definition.RelativeVirtualAddress == 0 && (definition.Attributes & MethodAttributes.Abstract) == 0 &&
            (definition.Attributes & MethodAttributes.PinvokeImpl) == 0 && (definition.ImplAttributes & MethodImplAttributes.InternalCall) == 0 &&
            (definition.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.Runtime)
            tags.Add("no IL body");

        // Signature-level pointer and byref-like usage.
        string signature = set.MethodSignature(method);
        if (signature.Contains('*')) tags.Add("unsafe pointer (signature)");
        if (signature.Contains("method ")) tags.Add("function pointer (signature)");
        if (signature.Contains("System.Span`1<") || signature.Contains("System.ReadOnlySpan`1<")) tags.Add("Span<T> (signature)");

        if (definition.RelativeVirtualAddress == 0 || body is null) return tags;
        MethodBodyBlock block = assembly.Pe.GetMethodBody(definition.RelativeVirtualAddress);
        foreach (ExceptionRegion region in block.ExceptionRegions)
            tags.Add(region.Kind switch
            {
                ExceptionRegionKind.Filter => "exception filter",
                ExceptionRegionKind.Finally => "try/finally",
                ExceptionRegionKind.Fault => "try/fault",
                _ => "try/catch",
            });
        if (!block.LocalSignature.IsNil)
        {
            if (!block.LocalVariablesInitialized) tags.Add("locals without localsinit ([SkipLocalsInit])");
            foreach (string local in Signatures.Locals(set, assembly, block.LocalSignature))
            {
                if (local.Contains(" pinned")) tags.Add("pinned local (fixed)");
                if (local.Contains('*')) tags.Add("unsafe pointer (local)");
                if (local.Contains("System.Span`1<") || local.Contains("System.ReadOnlySpan`1<")) tags.Add("Span<T> (local)");
            }
        }

        for (int i = 0; i < body.Count; i++)
        {
            Instruction instruction = body[i];
            string op = instruction.OpCode.Name!;
            if ((instruction.Prefixes & IlInstructions.Volatile) != 0) tags.Add("threading: volatile. prefix");
            if ((instruction.Prefixes & IlInstructions.Unaligned) != 0) tags.Add("unaligned. prefix");
            if ((instruction.Prefixes & IlInstructions.Tail) != 0) tags.Add("tail. prefix");
            if ((instruction.Prefixes & IlInstructions.Constrained) != 0) tags.Add("constrained. call");
            switch (op)
            {
                case "localloc": tags.Add("stackalloc (localloc)"); break;
                case "cpblk": case "initblk": tags.Add("raw memory block (cpblk/initblk)"); break;
                case "calli": tags.Add("indirect call (calli)"); break;
                case "ldftn": case "ldvirtftn": tags.Add("delegate or function pointer creation (ldftn)"); break;
                case "jmp": tags.Add("jmp"); break;
                case "arglist": case "mkrefany": case "refanyval": case "refanytype": tags.Add("varargs/typed reference"); break;
                case "ckfinite": tags.Add("ckfinite"); break;
                case "sizeof": tags.Add("sizeof"); break;
                case "ldtoken": tags.Add("reflection: ldtoken"); break;
                case "endfilter": break;
                case "rethrow": tags.Add("rethrow"); break;
                case "throw": tags.Add("throw"); break;
                case "box": case "unbox": case "unbox.any": tags.Add("boxing"); break;
                case "isinst": case "castclass": tags.Add("type test or cast"); break;
                case "newarr": tags.Add("array allocation"); break;
                case "ldelema": tags.Add("array element address"); break;
                case "conv.ovf.i": case "conv.ovf.u": case "conv.ovf.i.un": case "conv.ovf.u.un": tags.Add("checked native-int conversion"); break;
                case "add.ovf": case "add.ovf.un": case "sub.ovf": case "sub.ovf.un": case "mul.ovf": case "mul.ovf.un": tags.Add("checked arithmetic"); break;
                case "ldind.i": case "stind.i": case "ldind.i1": case "ldind.u1": case "ldind.i2": case "ldind.u2": case "ldind.i4":
                case "ldind.u4": case "ldind.i8": case "ldind.r4": case "ldind.r8": case "ldind.ref": case "stind.i1": case "stind.i2":
                case "stind.i4": case "stind.i8": case "stind.r4": case "stind.r8": case "stind.ref": case "ldobj": case "stobj":
                    tags.Add("indirect load/store"); break;
                case "ldloca": case "ldloca.s": case "ldarga": case "ldarga.s": case "ldflda": case "ldsflda":
                    tags.Add("managed address (ldloca/ldflda)"); break;
                case "switch": tags.Add("switch"); break;
                case "conv.r.un": case "conv.r4": case "conv.r8": case "ldc.r4": case "ldc.r8": tags.Add("floating point"); break;
                case "ldstr": tags.Add("string literal"); break;
            }
            if (instruction.Token == 0) continue;
            EntityHandle token = IlInstructions.Entity(instruction.Token);
            if (op is "call" or "callvirt" or "newobj")
            {
                MethodId? target = set.ResolveMethod(assembly, token, out bool viaSpec);
                if (viaSpec) tags.Add("generic method call");
                if (target is null) { tags.Add("unresolved member reference"); continue; }
                TypeFacts targetType = set.TypeOf(target.Value);
                string full = targetType.FullName;
                foreach (string threading in ThreadingTypes)
                    if (full.StartsWith(threading, StringComparison.Ordinal)) { tags.Add("threading: " + threading.TrimEnd('.').Replace("System.Threading.", "")); break; }
                if (full.StartsWith("System.Reflection.", StringComparison.Ordinal) ||
                    full == "System.Type" && set.MethodName(target.Value) is not ("GetTypeFromHandle" or "op_Equality" or "op_Inequality"))
                    tags.Add("reflection: API call");
                if (full == "System.Type" && set.MethodName(target.Value) == "GetTypeFromHandle") tags.Add("reflection: typeof");
                if (full == "System.Activator" || full.StartsWith("System.ComponentModel.TypeDescriptor", StringComparison.Ordinal))
                    tags.Add("reflection: activation (not modeled by RTA)");
                if (full.StartsWith("System.Runtime.InteropServices.Marshal", StringComparison.Ordinal)) tags.Add("interop: Marshal");
                if (full.StartsWith("System.Runtime.InteropServices.ComWrappers", StringComparison.Ordinal) ||
                    full.StartsWith("System.Runtime.InteropServices.Marshalling.", StringComparison.Ordinal)) tags.Add("interop: COM marshalling");
                if (full.StartsWith("System.Runtime.InteropServices.SafeHandle", StringComparison.Ordinal)) tags.Add("interop: SafeHandle");
                if (full == "System.Runtime.InteropServices.GCHandle") tags.Add("interop: GCHandle");
                if (full == "System.Runtime.CompilerServices.Unsafe" || full == "System.Runtime.InteropServices.MemoryMarshal")
                    tags.Add("unsafe: Unsafe/MemoryMarshal");
                if (full == "System.Runtime.CompilerServices.RuntimeHelpers") tags.Add("runtime helpers");
                if (full == "System.GC") tags.Add("GC API");
                if (full.StartsWith("Microsoft.Win32.Registry", StringComparison.Ordinal)) tags.Add("platform: registry");
                if (full.StartsWith("System.IO.File", StringComparison.Ordinal) || full.StartsWith("System.IO.Directory", StringComparison.Ordinal))
                    tags.Add("platform: filesystem");
                if (full.StartsWith("System.Resources.", StringComparison.Ordinal)) tags.Add("resources");
                if (full.StartsWith("System.Globalization.", StringComparison.Ordinal)) tags.Add("globalization");
                if (full == "System.Environment") tags.Add("platform: environment");
                if (targetType.IsComImport) tags.Add("interop: COM call");
                MethodDefinition targetDefinition = set.Definition(target.Value);
                if ((targetDefinition.Attributes & MethodAttributes.PinvokeImpl) != 0) tags.Add("calls P/Invoke");
                if (op == "callvirt" && targetType.IsInterface) tags.Add("interface call");
                else if (op == "callvirt" && (targetDefinition.Attributes & MethodAttributes.Virtual) != 0) tags.Add("virtual call");
                if (op == "newobj" && targetType.IsValueType) tags.Add("value-type newobj");
                if (op == "newobj" && targetType.IsDelegate) tags.Add("delegate allocation");
                if (op == "call" && targetType.IsValueType && (targetDefinition.Attributes & MethodAttributes.Static) == 0)
                    tags.Add("value-type instance call");
            }
            else if (op is "ldfld" or "ldflda" or "stfld" or "ldsfld" or "ldsflda" or "stsfld")
            {
                (TypeId? fieldOwner, _) = set.ResolveFieldOwner(assembly, token);
                if (fieldOwner is null) { tags.Add("unresolved field reference"); continue; }
                TypeFacts fieldType = set.Type(fieldOwner.Value);
                if (fieldType.IsExplicitLayout) tags.Add("explicit layout (field access)");
                if (op.StartsWith("lds") || op.StartsWith("sts"))
                {
                    tags.Add("static field");
                    if (fieldOwner.Value != owner.Id && fieldType.StaticConstructor is not null) tags.Add("foreign type initializer trigger");
                }
                if (fieldType.IsValueType && op is "ldfld" or "stfld" or "ldflda") tags.Add("value-type field");
            }
        }
        return tags;
    }

    public static string NormalizeDll(string dll)
    {
        string lower = dll.ToLowerInvariant();
        return lower.EndsWith(".dll", StringComparison.Ordinal) ? lower : lower + ".dll";
    }
}
