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
    private void Emit(string text) => body.AppendLine("    " + text);

    // A stack depth and its Wasm type identify a spill slot shared across incoming edges.
    // Materializing each result here preserves IL evaluation order, even across a dup or a call.
    private static string Slot(int depth, string type)
    {
        var suffix = type switch
        {
            Int => "i",
            IntArray => "a",
            ByteArray => "b",
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

    private string PopArray()
    {
        if (stack.Count == 0 || stack[^1] is not (IntArray or ByteArray))
            throw new InvalidDataException("Expected an array reference");
        return Pop(null);
    }

    private bool Translate(Instruction i)
    {
        var code = i.OpCode.Code;
        if (IntegerOps.Binary.TryGetValue(code, out var op))
        {
            var right = Pop();
            Push($"(i32.{op} {Pop()} {right})");
            return false;
        }
        switch (code)
        {
            case Code.Nop: break;
            case >= Code.Ldc_I4_M1 and <= Code.Ldc_I4_8:
                Push($"(i32.const {code - Code.Ldc_I4_0})"); break;
            case Code.Ldc_I4: case Code.Ldc_I4_S:
                Push($"(i32.const {i.Operand})"); break;
            case >= Code.Ldarg_0 and <= Code.Ldarg_3: case Code.Ldarg: case Code.Ldarg_S:
            {
                var index = i.Operand is ParameterDefinition p ? p.Index : code - Code.Ldarg_0;
                var type = method.Parameters[index].ParameterType;
                var value = $"(local.get $p{index})";
                Push(type.MetadataType == MetadataType.Byte ? NarrowByte(value) : value, ValueType(type));
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
                Emit($"(local.set $v{index} {(type.MetadataType == MetadataType.Byte ? NarrowByte(value) : value)})");
                break;
            }
            case Code.Pop: Pop(null); break;
            case Code.Dup: Push($"(local.get {Slot(stack.Count - 1, stack[^1])})", stack[^1]); break;
            case Code.Neg: Push($"(i32.sub (i32.const 0) {Pop()})"); break;
            case Code.Not: Push($"(i32.xor (i32.const -1) {Pop()})"); break;
            case Code.Conv_I4: case Code.Conv_U4: case Code.Conv_I: case Code.Conv_U:
                Push(Pop()); break;
            case Code.Conv_U1: Push(NarrowByte(Pop())); break;
            case Code.Rem:
            {
                var divisor = Pop();
                var dividend = Pop();
                // Wasm rem_s returns zero on this pair. CLR integer remainder can throw.
                Emit($"(if (i32.and (i32.eq {dividend} (i32.const -2147483648)) (i32.eq {divisor} (i32.const -1))) (then (unreachable)))");
                Push($"(i32.rem_s {dividend} {divisor})");
                break;
            }
            case Code.Newarr when ((TypeReference)i.Operand).FullName == "System.Int32":
                Push($"(array.new_default $ints {Pop()})", IntArray); break;
            case Code.Newarr when ((TypeReference)i.Operand).FullName == "System.Byte":
                Push($"(array.new_default $bytes {Pop()})", ByteArray); break;
            case Code.Ldlen: Push($"(array.len {PopArray()})"); break;
            case Code.Ldelem_U1:
            case Code.Ldelem_I1:
            {
                // A packed i8 cell needs the extension mode requested by the IL.
                var index = Pop();
                var instruction = code == Code.Ldelem_U1 ? "get_u" : "get_s";
                Push($"(array.{instruction} $bytes {Pop(ByteArray)} {index})");
                break;
            }
            case Code.Ldelem_I4:
            {
                var index = Pop();
                Push($"(array.get $ints {Pop(IntArray)} {index})"); break;
            }
            case Code.Stelem_I1:
            {
                var value = Pop();
                var index = Pop();
                // Wasm stores the low eight bits into the packed cell.
                Emit($"(array.set $bytes {Pop(ByteArray)} {index} {value})");
                break;
            }
            case Code.Stelem_I4:
            {
                var value = Pop();
                var index = Pop();
                Emit($"(array.set $ints {Pop(IntArray)} {index} {value})"); break;
            }
            case Code.Call:
            {
                var reference = (MethodReference)i.Operand;
                if (reference.DeclaringType.Scope != method.Module || reference is GenericInstanceMethod)
                    throw new NotSupportedException($"Unlinked call: {reference.FullName}");
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
            case Code.Ret:
            {
                var value = method.ReturnType.MetadataType == MetadataType.Void ? "" : Pop(ModuleCompiler.ValueType(method.ReturnType));
                if (stack.Count != 0) throw new InvalidDataException("Unexpected stack at return");
                if (method.ReturnType.MetadataType == MetadataType.Byte) value = NarrowByte(value);
                Emit($"(return {value})"); return true;
            }
            default:
                if (i.OpCode.FlowControl != FlowControl.Cond_Branch || i.Operand is not Instruction targetBlock)
                    throw new NotSupportedException("Unsupported IL instruction");
                string condition;
                if (code is Code.Brfalse or Code.Brfalse_S) condition = $"(i32.eqz {Pop()})";
                else if (code is Code.Brtrue or Code.Brtrue_S) condition = Pop();
                else
                {
                    var right = Pop();
                    condition = $"(i32.{IntegerOps.Comparison(code)} {Pop()} {right})";
                }
                Emit($"(if {condition} (then {Jump(targetBlock)}))");
                Emit(Jump(i.Next)); return true;
        }
        return false;
    }
}
