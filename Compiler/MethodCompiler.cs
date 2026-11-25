using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using static ModuleCompiler;

internal sealed class MethodCompiler(ModuleCompiler module, MethodDefinition method)
{
    private readonly Dictionary<Instruction, string[]> entries = new();
    private readonly Queue<Instruction> pending = new();
    private readonly HashSet<Instruction> leaders = new();
    private readonly Dictionary<string, string> temporaries = new();
    private readonly List<string> stack = new();
    private readonly StringBuilder body = new();
    // Set when Translate lowers two adjacent instructions as one, so that the block loop skips the second.
    private Instruction? fused;
    private void Emit(string text) => body.AppendLine("    " + text);

    // A stack depth and its Wasm type identify a spill slot shared across incoming edges.
    // Materializing each result here preserves IL evaluation order, even across a dup or a call.
    private static string Slot(int depth, string type)
    {
        var suffix = type switch
        {
            Int => "i",
            Long => "l",
            Float32 => "f",
            Float64 => "d",
            _ when ArrayKinds.ForReference(type) is { } kind => kind.TypeName[1..],
            _ => throw new InvalidDataException($"Invalid evaluation stack type: {type}")
        };
        return $"$s{depth}{suffix}";
    }
    private void Push(string expression, string type = Int)
    {
        var name = Slot(stack.Count, type);
        temporaries[name] = type;
        stack.Add(type);
        Emit($"(local.set {name} {expression})");
    }

    private string Pop(string? expected = Int)
    {
        var depth = stack.Count - 1;
        if (depth < 0) throw new InvalidDataException("Evaluation stack underflow");
        var type = stack[depth];
        if (expected != null && type != expected) throw new InvalidDataException("Stack type mismatch");
        stack.RemoveAt(depth);
        return $"(local.get {Slot(depth, type)})";
    }

    private void RegisterEdge(Instruction target)
    {
        if (entries.TryGetValue(target, out var types))
        {
            if (!types.SequenceEqual(stack)) throw new InvalidDataException("Incompatible incoming stacks");
            return;
        }
        entries.Add(target, stack.ToArray());
        pending.Enqueue(target);
    }

    private string Jump(Instruction target)
    {
        RegisterEdge(target);
        return $"(local.set $pc (i32.const {target.Offset})) (br $dispatch)";
    }

    internal string Compile(string name)
    {
        var instructions = method.Body.Instructions;
        foreach (var instruction in instructions)
        {
            if (instruction.Operand is Instruction target) leaders.Add(target);
            // Every switch table entry starts a basic block, like any other branch target.
            if (instruction.Operand is Instruction[] table) leaders.UnionWith(table);
            if (instruction.OpCode.FlowControl == FlowControl.Cond_Branch && instruction.Next != null)
                leaders.Add(instruction.Next);
        }
        RegisterEdge(instructions[0]);
        while (pending.TryDequeue(out var first))
        {
            stack.Clear();
            stack.AddRange(entries[first]);
            Emit($"(if (i32.eq (local.get $pc) (i32.const {first.Offset})) (then");
            for (var current = first; current != null; current = current.Next)
            {
                try
                {
                    if (current != first && leaders.Contains(current)) { Emit(Jump(current)); break; }
                    if (Translate(current)) break;
                    if (fused != null && fused == current.Next) { current = fused; fused = null; }
                }
                catch (Exception e)
                {
                    throw new NotSupportedException($"{method.FullName} at IL_{current.Offset:x4} ({current.OpCode}): {e.Message}", e);
                }
            }
            Emit("))");
        }
        var function = new StringBuilder($" (func {name}");
        foreach (var p in method.Parameters)
            function.Append($" (param $p{p.Index} {ModuleCompiler.ValueType(p.ParameterType)})");
        if (method.ReturnType.MetadataType != MetadataType.Void)
            function.Append($" (result {ModuleCompiler.ValueType(method.ReturnType)})");
        function.AppendLine(" (local $pc i32)");
        foreach (var v in method.Body.Variables)
            function.AppendLine($"  (local $v{v.Index} {ModuleCompiler.ValueType(v.VariableType)})");
        foreach (var (slot, type) in temporaries)
            function.AppendLine($"  (local {slot} {type})");
        return function.AppendLine("  (loop $dispatch").Append(body).AppendLine("   (unreachable)) (unreachable))").ToString();
    }

    private static string NarrowByte(string value) => $"(i32.and {value} (i32.const 255))";

    // A small integer type holds a narrower range than its i32 storage. Restore the CLR value wherever one enters or
    // leaves a typed location: sbyte and short sign-extend, while byte, ushort and char zero-extend (mask).
    private static string Normalize(TypeReference type, string value) => type.MetadataType switch
    {
        MetadataType.Byte => NarrowByte(value),
        MetadataType.SByte => $"(i32.extend8_s {value})",
        MetadataType.Int16 => $"(i32.extend16_s {value})",
        MetadataType.UInt16 or MetadataType.Char => $"(i32.and {value} (i32.const 65535))",
        _ => value
    };

    private string PopArray()
    {
        if (stack.Count == 0 || ArrayKinds.ForReference(stack[^1]) is null)
            throw new InvalidDataException("Expected an array reference");
        return Pop(null);
    }

    // Pops an int32 or int64 operand and reports which Wasm type it has. The IL binary numeric rule
    // (ECMA-335 III.1.5) never mixes int32 and int64 operands, so callers pop the other operand with
    // Pop(type), which rejects a mixture.
    private (string Value, string Type) PopInteger()
    {
        var type = stack.Count == 0 ? Int : stack[^1];
        if (type is not (Int or Long)) throw new InvalidDataException("Expected an integer operand");
        return (Pop(type), type);
    }

    // Pops any int32, int64, float32 or float64 operand together with its Wasm type.
    private (string Value, string Type) PopNumeric()
    {
        var type = stack.Count == 0 ? Int : stack[^1];
        if (type is not (Int or Long or Float32 or Float64)) throw new InvalidDataException("Expected a numeric operand");
        return (Pop(type), type);
    }

    // conv.i4, conv.u4, conv.i and conv.u: integers keep their low 32 bits, and floats truncate with saturation.
    private static string Narrow32((string Value, string Type) operand, bool unsigned = false) => operand.Type switch
    {
        Long => $"(i32.wrap_i64 {operand.Value})",
        Float32 or Float64 => FloatOps.Truncate(Int, unsigned, operand.Type, operand.Value),
        _ => operand.Value
    };

    // The CLR shift count is int32 for both operand widths. i64 shifts take an i64 count. Wasm masks the
    // count to the operand width, which matches C# semantics (Roslyn masks with 31 or 63 itself).
    private static bool IsShift(Code code) => code is Code.Shl or Code.Shr or Code.Shr_Un;

    private static bool IsComparison(Code code) =>
        code is Code.Ceq or Code.Clt or Code.Clt_Un or Code.Cgt or Code.Cgt_Un;

    private bool Translate(Instruction i)
    {
        var code = i.OpCode.Code;
        if (IntegerOps.Binary.TryGetValue(code, out var op))
        {
            if (!IsShift(code) && stack.Count > 0 && FloatOps.IsFloat(stack[^1]))
            {
                var (rightFloat, floatType) = PopNumeric();
                var leftFloat = Pop(floatType);
                // and, or, xor, div.un and rem.un have no floating-point form, so Compare rejects them.
                Push(FloatOps.Arithmetic.TryGetValue(code, out var arithmetic)
                    ? $"({floatType}.{arithmetic} {leftFloat} {rightFloat})"
                    : FloatOps.Compare(code, floatType, leftFloat, rightFloat),
                    FloatOps.Arithmetic.ContainsKey(code) ? floatType : Int);
                return false;
            }
            if (IsShift(code))
            {
                var count = Pop();
                var (shifted, width) = PopInteger();
                var amount = width == Long ? $"(i64.extend_i32_u {count})" : count;
                Push($"({width}.{op} {shifted} {amount})", width);
                return false;
            }
            var (right, type) = PopInteger();
            var left = Pop(type);
            // Comparisons yield an int32 0 or 1 whatever the operand width.
            Push($"({type}.{op} {left} {right})", IsComparison(code) ? Int : type);
            return false;
        }
        switch (code)
        {
            case Code.Nop: break;
            case >= Code.Ldc_I4_M1 and <= Code.Ldc_I4_8:
                Push($"(i32.const {code - Code.Ldc_I4_0})"); break;
            case Code.Ldc_I4: case Code.Ldc_I4_S:
                Push($"(i32.const {i.Operand})"); break;
            case Code.Ldc_I8:
                Push($"(i64.const {i.Operand})", Long); break;
            case Code.Ldc_R4:
                Push(FloatOps.Literal((float)i.Operand), Float32); break;
            case Code.Ldc_R8:
                Push(FloatOps.Literal((double)i.Operand), Float64); break;
            case >= Code.Ldarg_0 and <= Code.Ldarg_3: case Code.Ldarg: case Code.Ldarg_S:
            {
                var index = i.Operand is ParameterDefinition p ? p.Index : code - Code.Ldarg_0;
                var type = method.Parameters[index].ParameterType;
                var value = $"(local.get $p{index})";
                Push(Normalize(type, value), ValueType(type));
                break;
            }
            case Code.Starg: case Code.Starg_S:
            {
                var p = (ParameterDefinition)i.Operand;
                Emit($"(local.set $p{p.Index} {Pop(ModuleCompiler.ValueType(p.ParameterType))})");
                break;
            }
            case >= Code.Ldloc_0 and <= Code.Ldloc_3: case Code.Ldloc: case Code.Ldloc_S:
            {
                var index = i.Operand is VariableDefinition v ? v.Index : code - Code.Ldloc_0;
                Push($"(local.get $v{index})", ModuleCompiler.ValueType(method.Body.Variables[index].VariableType));
                break;
            }
            case >= Code.Stloc_0 and <= Code.Stloc_3: case Code.Stloc: case Code.Stloc_S:
            {
                var index = i.Operand is VariableDefinition v ? v.Index : code - Code.Stloc_0;
                var type = method.Body.Variables[index].VariableType;
                var value = Pop(ValueType(type));
                Emit($"(local.set $v{index} {Normalize(type, value)})");
                break;
            }
            case Code.Pop: Pop(null); break;
            case Code.Dup: Push($"(local.get {Slot(stack.Count - 1, stack[^1])})", stack[^1]); break;
            case Code.Neg:
            {
                var (value, type) = PopNumeric();
                // Float32 negation flips the sign bit, so -(0.0) is -0.0. Subtraction from zero would give +0.0.
                Push(FloatOps.IsFloat(type) ? $"({type}.neg {value})" : $"({type}.sub ({type}.const 0) {value})", type); break;
            }
            case Code.Not:
            {
                var (value, type) = PopInteger();
                Push($"({type}.xor ({type}.const -1) {value})", type); break;
            }
            // Native int is 32 bits in this wasm32 mapping. Narrowing int64 keeps the low 32 bits, as the CLR does.
            case Code.Conv_I4: case Code.Conv_U4: case Code.Conv_I: case Code.Conv_U:
                Push(Narrow32(PopNumeric(), code is Code.Conv_U4 or Code.Conv_U)); break;
            // Conversions to small types truncate the int32 (or saturated float) value to the target width.
            case Code.Conv_U1: Push(NarrowByte(Narrow32(PopNumeric()))); break;
            case Code.Conv_I1: Push($"(i32.extend8_s {Narrow32(PopNumeric())})"); break;
            case Code.Conv_I2: Push($"(i32.extend16_s {Narrow32(PopNumeric())})"); break;
            case Code.Conv_U2: Push($"(i32.and {Narrow32(PopNumeric())} (i32.const 65535))"); break;
            case Code.Conv_I8: case Code.Conv_U8:
            {
                // ECMA-335 III.3.27: conv.i8 sign-extends an int32 and conv.u8 zero-extends it. int64 passes through.
                var (value, type) = PopNumeric();
                var extend = code == Code.Conv_I8 ? "i64.extend_i32_s" : "i64.extend_i32_u";
                Push(type switch
                {
                    Long => value,
                    Float32 or Float64 => FloatOps.Truncate(Long, code == Code.Conv_U8, type, value),
                    _ => $"({extend} {value})"
                }, Long); break;
            }
            case Code.Conv_R4: case Code.Conv_R8: case Code.Conv_R_Un:
            {
                var (value, type) = PopNumeric();
                if (code == Code.Conv_R_Un && FloatOps.IsFloat(type))
                    throw new InvalidDataException("conv.r.un expects an integer operand");
                var target = code == Code.Conv_R4 ? Float32 : Float64;
                // C# lowers (float)ulong to "conv.r.un; conv.r4", which by the IL rounds twice (through F/double).
                // RyuJIT converts that adjacent pair in one rounding. For 0x8000008000000001 it returns
                // 2^63 + 2^40, not 2^63. To match the CLR on the same DLL, fuse the pair when the conv.r4 is not
                // a branch target. A separate conv.r8 or a stored double still rounds twice, as on the CLR.
                if (code == Code.Conv_R_Un && i.Next?.OpCode.Code == Code.Conv_R4 && !leaders.Contains(i.Next))
                {
                    target = Float32;
                    fused = i.Next;
                }
                Push(FloatOps.Convert(target, code == Code.Conv_R_Un, type, value), target); break;
            }
            case Code.Rem:
            {
                if (stack.Count > 0 && FloatOps.IsFloat(stack[^1]))
                {
                    // CLR float remainder is C fmod (truncated, sign of the dividend). Wasm has no instruction for it,
                    // so call the exact runtime helper. A float32 remainder is exactly representable as float32, so
                    // computing it in f64 and demoting is exact.
                    var (floatDivisor, floatType) = PopNumeric();
                    var floatDividend = Pop(floatType);
                    var fmod = module.RequireHelper("fmod");
                    Push(floatType == Float64
                        ? $"(call {fmod} {floatDividend} {floatDivisor})"
                        : $"(f32.demote_f64 (call {fmod} (f64.promote_f32 {floatDividend}) (f64.promote_f32 {floatDivisor})))",
                        floatType);
                    break;
                }
                var (divisor, type) = PopInteger();
                var dividend = Pop(type);
                // Wasm rem_s returns zero for MinValue % -1. The CLR throws OverflowException, so trap explicitly.
                var minimum = type == Long ? "-9223372036854775808" : "-2147483648";
                Emit($"(if (i32.and ({type}.eq {dividend} ({type}.const {minimum})) ({type}.eq {divisor} ({type}.const -1))) (then (unreachable)))");
                Push($"({type}.rem_s {dividend} {divisor})", type);
                break;
            }
            case Code.Newarr:
            {
                // new T[n] zero-initializes like the CLR. A negative n traps where the CLR throws OverflowException.
                var element = (TypeReference)i.Operand;
                var kind = ArrayKinds.ForElement(element.FullName)
                    ?? throw new NotSupportedException($"Arrays of {element.FullName} are not supported");
                Push($"(array.new_default {kind.TypeName} {Pop()})", kind.Reference); break;
            }
            case Code.Ldlen: Push($"(array.len {PopArray()})"); break;
            case var _ when ArrayKinds.Load(code) is { } load:
            {
                // Bounds and null checks trap where the CLR throws IndexOutOfRange/NullReference.
                var index = Pop();
                Push($"({load.Accessor} {load.Kind.TypeName} {Pop(load.Kind.Reference)} {index})", load.Kind.Element);
                break;
            }
            case var _ when ArrayKinds.Store(code) is { } store:
            {
                var value = Pop(store.Element);
                var index = Pop();
                Emit($"(array.set {store.TypeName} {Pop(store.Reference)} {index} {value})");
                break;
            }
            case Code.Call:
            {
                var reference = (MethodReference)i.Operand;
                if (BclIntrinsics.Find(reference) is { } intrinsic)
                {
                    // Pop arguments right to left with their declared Wasm types, then emit the guard and the value.
                    var values = intrinsic.Parameters.Reverse().Select(type => Pop(type)).Reverse().ToArray();
                    var (guard, value) = intrinsic.Lower(values);
                    if (guard != null) Emit(guard);
                    Push(value, intrinsic.Result);
                    break;
                }
                if (reference.DeclaringType.Scope != method.Module || reference is GenericInstanceMethod)
                    throw new NotSupportedException($"Unlinked call: {reference.FullName} in {reference.DeclaringType.Scope.Name}");
                var target = reference.Resolve();
                var callee = module.Register(target);
                var arguments = target.Parameters.Reverse().Select(p => Pop(ModuleCompiler.ValueType(p.ParameterType))).Reverse().ToArray();
                var operands = string.Join(" ", arguments);
                if (i.Next?.OpCode.Code == Code.Ret && stack.Count == 0 && method.ReturnType.FullName == target.ReturnType.FullName)
                {
                    Emit($"(return_call {callee} {operands})"); return true;
                }
                var call = $"(call {callee} {operands})";
                if (target.ReturnType.MetadataType == MetadataType.Void) Emit(call);
                else Push(call, ModuleCompiler.ValueType(target.ReturnType));
                break;
            }
            case Code.Br: case Code.Br_S: Emit(Jump((Instruction)i.Operand)); return true;
            case Code.Switch:
            {
                // ECMA-335 III.3.66: jump to table[value] when (uint)value < N, otherwise fall through. Wasm br_table
                // also reads its index as unsigned and takes the last label for out-of-range values. Exiting nested
                // block k runs the code for case k, which records its IL target in $pc and re-enters the dispatcher.
                // The rest of the stack flows to every target unchanged, and Jump checks that each one agrees.
                var table = (Instruction[])i.Operand;
                var value = Pop();
                string Label(string suffix) => $"$switch{i.Offset:x}_{suffix}";
                Emit($"(block {Label("default")}");
                for (var k = table.Length - 1; k >= 0; k--) Emit($"(block {Label(k.ToString())}");
                var labels = string.Join(" ", Enumerable.Range(0, table.Length).Select(k => Label(k.ToString())));
                Emit($"(br_table {labels} {Label("default")} {value})");
                for (var k = 0; k < table.Length; k++) Emit($") {Jump(table[k])}");
                Emit(")");
                Emit(Jump(i.Next)); return true;
            }
            case Code.Ret:
            {
                var value = method.ReturnType.MetadataType == MetadataType.Void ? "" : Pop(ModuleCompiler.ValueType(method.ReturnType));
                if (stack.Count != 0) throw new InvalidDataException("Unexpected stack at return");
                value = Normalize(method.ReturnType, value);
                Emit($"(return {value})"); return true;
            }
            default:
                if (i.OpCode.FlowControl != FlowControl.Cond_Branch || i.Operand is not Instruction targetBlock)
                    throw new NotSupportedException("Unsupported IL instruction");
                string condition;
                if (code is Code.Brfalse or Code.Brfalse_S or Code.Brtrue or Code.Brtrue_S)
                {
                    // brtrue and brfalse accept any integer width, so test against zero at the operand's width.
                    var (value, type) = PopInteger();
                    if (code is Code.Brfalse or Code.Brfalse_S) condition = $"({type}.eqz {value})";
                    else condition = type == Long ? $"(i64.ne {value} (i64.const 0))" : value;
                }
                else
                {
                    var (right, type) = PopNumeric();
                    var left = Pop(type);
                    condition = FloatOps.IsFloat(type) ? FloatOps.Branch(code, type, left, right)
                        : $"({type}.{IntegerOps.Comparison(code)} {left} {right})";
                }
                Emit($"(if {condition} (then {Jump(targetBlock)}))");
                Emit(Jump(i.Next)); return true;
        }
        return false;
    }
}
