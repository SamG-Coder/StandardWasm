using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;

internal static class IlOffsets
{
    private static readonly Dictionary<short, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);

    internal static int Find(byte[] il, OpCode target)
    {
        for (int cursor = 0; cursor < il.Length;)
        {
            int offset = cursor;
            int first = il[cursor++];
            short value = first == 0xfe ? unchecked((short)(0xfe00 | il[cursor++])) : (short)first;
            if (value == target.Value) return offset;
            cursor += Opcodes[value].OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(cursor)),
                _ => 4
            };
        }
        return -1;
    }
}
