using Mono.Cecil;
using Mono.Cecil.Cil;
using static ModuleCompiler;

// One-dimensional primitive arrays (vectors). Each CLR element type maps to a Wasm GC array whose storage keeps the
// element's width: i8 and i16 are packed storage types, so a store truncates exactly like a CLR store into a byte or
// short cell, and the IL load opcode chooses sign or zero extension (array.get_s / array.get_u). Signed and
// unsigned element types of one width share an array type because the IL instruction carries the signedness.
internal static class ArrayKinds
{
    internal sealed record Kind(string TypeName, string Storage, string Element)
    {
        // Wasm reference type used for locals, parameters and stack slots. Nullable, like a CLR array reference.
        internal string Reference => $"(ref null {TypeName})";
        internal string Declaration => $" (type {TypeName} (array (mut {Storage})))";
    }

    internal static readonly Kind Ints = new("$ints", "i32", Int);
    internal static readonly Kind Bytes = new("$bytes", "i8", Int);
    internal static readonly Kind Shorts = new("$shorts", "i16", Int);
    internal static readonly Kind Longs = new("$longs", "i64", Long);
    internal static readonly Kind Floats = new("$floats", "f32", Float32);
    internal static readonly Kind Doubles = new("$doubles", "f64", Float64);
    internal static readonly Kind[] All = [Ints, Bytes, Shorts, Longs, Floats, Doubles];

    // The element must be a platform primitive by CLI identity (PlatformTypes), not merely by name.
    internal static Kind? ForElement(TypeReference element) => PlatformTypes.Primitive(element) switch
    {
        MetadataType.Int32 or MetadataType.UInt32 => Ints,
        MetadataType.Byte or MetadataType.SByte or MetadataType.Boolean => Bytes,
        MetadataType.Int16 or MetadataType.UInt16 or MetadataType.Char => Shorts,
        MetadataType.Int64 or MetadataType.UInt64 => Longs,
        MetadataType.Single => Floats,
        MetadataType.Double => Doubles,
        _ => null
    };

    internal static Kind? ForReference(string wasmType) => All.FirstOrDefault(kind => kind.Reference == wasmType);

    // ldelem.<type>: the array kind it reads and the accessor that extends packed cells as the IL requests.
    internal static (Kind Kind, string Accessor)? Load(Code code) => code switch
    {
        Code.Ldelem_I1 => (Bytes, "array.get_s"),
        Code.Ldelem_U1 => (Bytes, "array.get_u"),
        Code.Ldelem_I2 => (Shorts, "array.get_s"),
        Code.Ldelem_U2 => (Shorts, "array.get_u"),
        Code.Ldelem_I4 or Code.Ldelem_U4 => (Ints, "array.get"),
        Code.Ldelem_I8 => (Longs, "array.get"),
        Code.Ldelem_R4 => (Floats, "array.get"),
        Code.Ldelem_R8 => (Doubles, "array.get"),
        _ => null
    };

    // stelem.<type>: the array kind it writes. Packed i8/i16 cells keep the low bits of the int32 value.
    internal static Kind? Store(Code code) => code switch
    {
        Code.Stelem_I1 => Bytes,
        Code.Stelem_I2 => Shorts,
        Code.Stelem_I4 => Ints,
        Code.Stelem_I8 => Longs,
        Code.Stelem_R4 => Floats,
        Code.Stelem_R8 => Doubles,
        _ => null
    };
}
