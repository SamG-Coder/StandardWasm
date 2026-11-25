using Mono.Cecil.Cil;

internal static class IntegerOps
{
    internal static readonly Dictionary<Code, string> Binary = new()
    {
        [Code.Add]="add", [Code.Sub]="sub", [Code.Mul]="mul", [Code.Div]="div_s",
        [Code.Div_Un]="div_u", [Code.Rem_Un]="rem_u",
        [Code.And]="and", [Code.Or]="or", [Code.Xor]="xor", [Code.Shl]="shl",
        [Code.Shr]="shr_s", [Code.Shr_Un]="shr_u", [Code.Ceq]="eq",
        [Code.Clt]="lt_s", [Code.Clt_Un]="lt_u", [Code.Cgt]="gt_s", [Code.Cgt_Un]="gt_u"
    };

    internal static string Comparison(Code code) => code switch
    {
        Code.Beq or Code.Beq_S => "eq", Code.Bne_Un or Code.Bne_Un_S => "ne",
        Code.Blt or Code.Blt_S => "lt_s", Code.Blt_Un or Code.Blt_Un_S => "lt_u",
        Code.Bgt or Code.Bgt_S => "gt_s", Code.Bgt_Un or Code.Bgt_Un_S => "gt_u",
        Code.Ble or Code.Ble_S => "le_s", Code.Ble_Un or Code.Ble_Un_S => "le_u",
        Code.Bge or Code.Bge_S => "ge_s", Code.Bge_Un or Code.Bge_Un_S => "ge_u",
        _ => throw new NotSupportedException($"Unsupported comparison {code}")
    };
}
