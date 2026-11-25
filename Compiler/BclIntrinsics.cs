using Mono.Cecil;
using static ModuleCompiler;

// BCL methods whose .NET behavior is exactly a Wasm instruction or a short guarded sequence (decision 0003).
// An entry is allowed only when it is bit-identical to the oracle runtime (.NET 10) for every input, with NaN
// payloads aside. Everything else stays an "Unlinked call" diagnostic until an implementation is compiled in.
internal static class BclIntrinsics
{
    // Parameter Wasm types, result Wasm type, and a lowering that receives side-effect-free operand expressions
    // (stack slot reads, so they may be repeated). Guard is emitted before the value and traps where .NET throws.
    internal sealed record Intrinsic(string[] Parameters, string Result, Func<string[], (string? Guard, string Value)> Lower);

    private static Intrinsic Unary(string type, string instruction) =>
        new([type], type, a => (null, $"({type}.{instruction} {a[0]})"));

    private static Intrinsic Binary(string type, string instruction) =>
        new([type, type], type, a => (null, $"({type}.{instruction} {a[0]} {a[1]})"));

    // Integer Max/Min pick an operand with the signed or unsigned comparison matching the C# parameter type.
    private static Intrinsic Select(string type, string comparison) =>
        new([type, type], type, a => (null, $"(select {a[0]} {a[1]} ({type}.{comparison} {a[0]} {a[1]}))"));

    // Math.Abs(int.MinValue) and Math.Abs(long.MinValue) throw OverflowException. Wasm traps instead.
    private static Intrinsic IntegerAbs(string type, string minimum) =>
        new([type], type, a => ($"(if ({type}.eq {a[0]} ({type}.const {minimum})) (then (unreachable)))",
            $"(select ({type}.sub ({type}.const 0) {a[0]}) {a[0]} ({type}.lt_s {a[0]} ({type}.const 0)))"));

    private static readonly Dictionary<string, Intrinsic> Table = new()
    {
        // IEEE 754 square root is correctly rounded in both runtimes.
        ["System.Double System.Math::Sqrt(System.Double)"] = Unary(Float64, "sqrt"),
        ["System.Double System.Math::Abs(System.Double)"] = Unary(Float64, "abs"),
        ["System.Single System.Math::Abs(System.Single)"] = Unary(Float32, "abs"),
        ["System.Double System.Math::Floor(System.Double)"] = Unary(Float64, "floor"),
        ["System.Double System.Math::Ceiling(System.Double)"] = Unary(Float64, "ceil"),
        ["System.Double System.Math::Truncate(System.Double)"] = Unary(Float64, "trunc"),
        // Math.Round(double) uses MidpointRounding.ToEven, which is IEEE roundTiesToEven (f64.nearest).
        ["System.Double System.Math::Round(System.Double)"] = Unary(Float64, "nearest"),
        // .NET Core 3.0+ Max/Min return NaN if either operand is NaN and treat -0 as less than +0, as Wasm does.
        ["System.Double System.Math::Max(System.Double,System.Double)"] = Binary(Float64, "max"),
        ["System.Double System.Math::Min(System.Double,System.Double)"] = Binary(Float64, "min"),
        ["System.Single System.Math::Max(System.Single,System.Single)"] = Binary(Float32, "max"),
        ["System.Single System.Math::Min(System.Single,System.Single)"] = Binary(Float32, "min"),
        ["System.Int32 System.Math::Max(System.Int32,System.Int32)"] = Select(Int, "gt_s"),
        ["System.Int32 System.Math::Min(System.Int32,System.Int32)"] = Select(Int, "lt_s"),
        ["System.UInt32 System.Math::Max(System.UInt32,System.UInt32)"] = Select(Int, "gt_u"),
        ["System.UInt32 System.Math::Min(System.UInt32,System.UInt32)"] = Select(Int, "lt_u"),
        ["System.Int64 System.Math::Max(System.Int64,System.Int64)"] = Select(Long, "gt_s"),
        ["System.Int64 System.Math::Min(System.Int64,System.Int64)"] = Select(Long, "lt_s"),
        ["System.UInt64 System.Math::Max(System.UInt64,System.UInt64)"] = Select(Long, "gt_u"),
        ["System.UInt64 System.Math::Min(System.UInt64,System.UInt64)"] = Select(Long, "lt_u"),
        ["System.Int32 System.Math::Abs(System.Int32)"] = IntegerAbs(Int, "-2147483648"),
        ["System.Int64 System.Math::Abs(System.Int64)"] = IntegerAbs(Long, "-9223372036854775808"),
    };

    // Only references into the BCL qualify. A type named System.Math inside the input assembly is compiled normally.
    internal static Intrinsic? Find(MethodReference reference, ModuleDefinition inputModule) =>
        reference.DeclaringType.Scope != inputModule && reference is not GenericInstanceMethod &&
        Table.TryGetValue(reference.FullName, out var intrinsic) ? intrinsic : null;
}
