using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace WinFormsCensus;

/// <summary>One decoded CIL instruction. Token is 0 for operands that are not metadata tokens.</summary>
internal readonly record struct Instruction(int Offset, OpCode OpCode, int Token, int Prefixes);

/// <summary>
/// Decodes ECMA-335 III instruction streams with operand sizes taken from
/// System.Reflection.Emit.OpCodes. This is a host analysis helper, independent
/// of the compiler's own BranchIl decoder.
/// </summary>
internal static class IlInstructions
{
    public const int Constrained = 1, Volatile = 2, Unaligned = 4, Tail = 8, Readonly = 16, No = 32;

    private static readonly OpCode?[] OneByte = new OpCode?[256];
    private static readonly OpCode?[] TwoByte = new OpCode?[256];

    static IlInstructions()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opcode = (OpCode)field.GetValue(null)!;
            ushort value = unchecked((ushort)opcode.Value);
            if (opcode.Size == 1) OneByte[value] = opcode;
            else TwoByte[value & 0xff] = opcode;
        }
    }

    public static List<Instruction> Decode(MethodBodyBlock body)
    {
        var result = new List<Instruction>();
        BlobReader reader = body.GetILReader();
        int prefixes = 0;
        while (reader.RemainingBytes > 0)
        {
            int offset = reader.Offset;
            byte first = reader.ReadByte();
            OpCode? decoded = first == 0xfe ? TwoByte[reader.ReadByte()] : OneByte[first];
            if (decoded is null) throw new BadImageFormatException($"Unknown opcode at IL_{offset:x4}");
            OpCode opcode = decoded.Value;
            int token = 0;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: reader.ReadByte(); break;
                case OperandType.InlineVar: reader.ReadUInt16(); break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.ShortInlineR: reader.ReadInt32(); break;
                case OperandType.InlineI8:
                case OperandType.InlineR: reader.ReadInt64(); break;
                case OperandType.InlineSwitch:
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++) reader.ReadInt32();
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    token = reader.ReadInt32();
                    break;
                default:
                    throw new BadImageFormatException("Unknown operand type " + opcode.OperandType);
            }
            if (opcode.OpCodeType == OpCodeType.Prefix)
            {
                prefixes |= opcode.Name switch
                {
                    "constrained." => Constrained,
                    "volatile." => Volatile,
                    "unaligned." => Unaligned,
                    "tail." => Tail,
                    "readonly." => Readonly,
                    _ => No,
                };
                result.Add(new Instruction(offset, opcode, token, 0));
                continue;
            }
            result.Add(new Instruction(offset, opcode, token, prefixes));
            prefixes = 0;
        }
        return result;
    }

    // User-string (0x70) and signature tokens are not entity handles for
    // member resolution; they map to a nil handle that resolves to nothing.
    public static EntityHandle Entity(int token) =>
        (token >> 24) is 0x70 or 0x11 ? default : MetadataTokens.EntityHandle(token);
}
