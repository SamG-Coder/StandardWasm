using System.Globalization;
using Mono.Cecil.Cil;
using static ModuleCompiler;

// Floating-point lowering. IL has one F stack type; this compiler keeps float32 values as f32 and float64 values
// as f64. The CLR on x64/arm64 computes float32 arithmetic in single precision and rounds at every store, so each
// f32 Wasm operation rounds where the CLR does. Both sides implement IEEE 754 round-to-nearest-even for
// add, sub, mul, div and the int <-> float conversions.
internal static class FloatOps
{
    internal static bool IsFloat(string type) => type is Float32 or Float64;

    internal static readonly Dictionary<Code, string> Arithmetic = new()
    {
        [Code.Add] = "add", [Code.Sub] = "sub", [Code.Mul] = "mul", [Code.Div] = "div"
    };

    // Ordered comparisons (ceq, clt, cgt and the plain branches) are false when either operand is NaN, in IL and
    // in Wasm. On floats the IL ".un" forms mean "unordered or ...". ECMA-335 III.1.5 defines them, for example
    // clt.un as !(a >= b), so each one lowers to the negated complementary ordered comparison. bne.un is Wasm ne,
    // which is already true for NaN.
    internal static string Compare(Code code, string type, string left, string right) => code switch
    {
        Code.Ceq => $"({type}.eq {left} {right})",
        Code.Clt => $"({type}.lt {left} {right})",
        Code.Cgt => $"({type}.gt {left} {right})",
        Code.Clt_Un => $"(i32.eqz ({type}.ge {left} {right}))",
        Code.Cgt_Un => $"(i32.eqz ({type}.le {left} {right}))",
        _ => throw new NotSupportedException($"{code} is not defined for floating-point operands")
    };

    internal static string Branch(Code code, string type, string left, string right) => code switch
    {
        Code.Beq or Code.Beq_S => $"({type}.eq {left} {right})",
        Code.Bne_Un or Code.Bne_Un_S => $"({type}.ne {left} {right})",
        Code.Blt or Code.Blt_S => $"({type}.lt {left} {right})",
        Code.Bgt or Code.Bgt_S => $"({type}.gt {left} {right})",
        Code.Ble or Code.Ble_S => $"({type}.le {left} {right})",
        Code.Bge or Code.Bge_S => $"({type}.ge {left} {right})",
        Code.Blt_Un or Code.Blt_Un_S => $"(i32.eqz ({type}.ge {left} {right}))",
        Code.Bgt_Un or Code.Bgt_Un_S => $"(i32.eqz ({type}.le {left} {right}))",
        Code.Ble_Un or Code.Ble_Un_S => $"(i32.eqz ({type}.gt {left} {right}))",
        Code.Bge_Un or Code.Bge_Un_S => $"(i32.eqz ({type}.lt {left} {right}))",
        _ => throw new NotSupportedException($"Unsupported comparison {code}")
    };

    // Literals keep their exact value. Finite values print with 9 (binary32) or 17 (binary64) significant digits,
    // which always identify the value uniquely, and WAT parses that back to the same bits. Do not use "R": on .NET 10,
    // 2^-958.ToString("R") yields 4.104536801298376E-289, which parses to a different double. NaN goes through its
    // bit pattern so the payload is preserved.
    internal static string Literal(float value) =>
        float.IsNaN(value) ? $"(f32.reinterpret_i32 (i32.const {BitConverter.SingleToInt32Bits(value)}))"
        : $"(f32.const {Finite(value, value.ToString("G9", CultureInfo.InvariantCulture))})";

    internal static string Literal(double value) =>
        double.IsNaN(value) ? $"(f64.reinterpret_i64 (i64.const {BitConverter.DoubleToInt64Bits(value)}))"
        : $"(f64.const {Finite(value, value.ToString("G17", CultureInfo.InvariantCulture))})";

    private static string Finite(double value, string text) =>
        double.IsPositiveInfinity(value) ? "inf" : double.IsNegativeInfinity(value) ? "-inf" : text;

    // .NET 9 and later define float -> integer conversions as saturating on every platform: NaN becomes 0 and
    // out-of-range values clamp to the target range. Wasm trunc_sat has exactly these semantics. Earlier .NET
    // versions produced platform-specific values, and the CLR oracle in this repository runs on .NET 10.
    internal static string Truncate(string target, bool unsigned, string source, string value) =>
        $"({target}.trunc_sat_{source}_{(unsigned ? "u" : "s")} {value})";

    // conv.r4, conv.r8 and conv.r.un from any numeric stack type. Integer sources round once to the target precision.
    // conv.r.un treats an integer source as unsigned and yields F, which this compiler keeps as f64. C# emits it
    // before conv.r4 for (float)ulong, so that path rounds twice, exactly as the IL specifies.
    internal static string Convert(string target, bool unsigned, string source, string value)
    {
        if (source == target) return value;
        if (source == Float64) return $"(f32.demote_f64 {value})";
        if (source == Float32) return $"(f64.promote_f32 {value})";
        return $"({target}.convert_{source}_{(unsigned ? "u" : "s")} {value})";
    }
}
